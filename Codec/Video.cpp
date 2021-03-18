#include "pch.h"

#include "Video.h"
#include "Utils.h"

using namespace System;
using namespace System::Diagnostics;
using namespace System::Runtime::InteropServices;

namespace Codec
{
	const AVPixelFormat outputPixelFormat = AVPixelFormat::AV_PIX_FMT_BGRA;

	struct StreamWrapper
	{
		gcroot<MemoryStream ^> stream;

		StreamWrapper(MemoryStream ^stream)
		{
			this->stream = stream;
		}
	};

	int ReadPacket(void *opaque, uint8_t *buffer, int bufferSize)
	{
		MemoryStream ^stream = ((StreamWrapper *)opaque)->stream;

		array<unsigned char> ^managedBuffer = gcnew array<unsigned char>(bufferSize);
		int bytesRead = stream->Read(managedBuffer, 0, bufferSize);

		Marshal::Copy(managedBuffer, 0, IntPtr(buffer), bytesRead);

		return bytesRead;
	}

	int64_t Seek(void *opaque, int64_t offset, int whence)
	{
		MemoryStream ^stream = ((StreamWrapper *)opaque)->stream;

		if (whence <= SEEK_END) {
			try {
				return stream->Seek(offset, (SeekOrigin)whence);
			} catch (Exception ^) {
				return -1;
			}
		} else if (whence == AVSEEK_SIZE) {
			return stream->Length;
		} else {
			return stream->Position;
		}
	}

	AVIOContext *CreateInputContext(MemoryStream ^stream)
	{
		const int bufferSize = 4096; // Use a 4 KB buffer
		uint8_t *buffer = (uint8_t *)av_malloc(bufferSize);

		AVIOContext *inputContext = avio_alloc_context(buffer, bufferSize, 0, new StreamWrapper(stream), ReadPacket, NULL, Seek);
		if (!inputContext) {
			av_free(buffer);
			throw gcnew Exception("Failed to create input context");
		}

		return inputContext;
	}

	static AVStream *OpenCodecContext(AVFormatContext *formatContext, enum AVMediaType type)
	{
		int streamIndex = av_find_best_stream(formatContext, type, -1, -1, NULL, 0);
		if (streamIndex < 0) {
			Debug::WriteLine(String::Format("No {0} stream was found", gcnew String(av_get_media_type_string(type))));
			return NULL;
		}

		AVStream *stream = formatContext->streams[streamIndex];

		// Find a decoder for the stream
		AVCodecContext *decoderContext = stream->codec;
		AVCodec *decoder = avcodec_find_decoder(decoderContext->codec_id);
		if (!decoder) {
			throw gcnew Exception(String::Format("Failed to find {0} codec", gcnew String(av_get_media_type_string(type))));
		}

		AVDictionary *opts = NULL;
		if ((avcodec_open2(decoderContext, decoder, &opts)) < 0) {
			throw gcnew Exception(String::Format("Failed to open {0} codec", gcnew String(av_get_media_type_string(type))));
		}

		return stream;
	}

	static void FreeFrame(AVFrame *frame)
	{
		if (frame) {
			av_frame_free(&frame);
		}
	}

	Video::Video(MemoryStream ^videoBuffer, int width, int height)
	{
		if (videoBuffer->Length == 0)
			return;

		this->videoBuffer = videoBuffer;
		videoBuffer->Position = 0;

		this->width = width;
		this->height = height;

		// Initialize libavcodec and register all codecs and formats
		av_register_all();

		AVFormatContext *formatContext = avformat_alloc_context();
		if (!formatContext) {
			throw gcnew Exception("Failed to allocate format context");
		}

		this->formatContext = formatContext;

		formatContext->pb = CreateInputContext(videoBuffer);

		if (avformat_open_input(&formatContext, NULL, NULL, NULL) < 0) {
			this->formatContext = NULL; // The format context is automatically freed on failure, so null it out to prevent our finalizer from freeing it again
			throw gcnew Exception("Failed to open format context");
		}

		if (avformat_find_stream_info(formatContext, NULL) < 0) {
			throw gcnew Exception("Failed to find stream info");
		}

		AVStream *videoStream = OpenCodecContext(formatContext, AVMEDIA_TYPE_VIDEO);
		if (videoStream) {
			this->videoStreamIndex = videoStream->index;
			this->duration = videoStream->duration;

			AVRational timeBaseRational = videoStream->time_base;
			this->timeBase = (double)timeBaseRational.num / timeBaseRational.den;

			this->decoderContext = videoStream->codec;

			this->frame = av_frame_alloc();
			if (!frame) {
				throw gcnew Exception("Failed to allocate frame");
			}

			this->tempFrame = AllocPicture(outputPixelFormat, width, height);

			// Initialize the packet
			av_init_packet(packet);
			packet->data = NULL;
			packet->size = 0;
		} else {
			throw gcnew Exception("No video stream was found");
		}
	}

	Video::~Video()
	{
		this->!Video();
	}

	Video::!Video()
	{
		FreeFrame(frame);
		FreeFrame(tempFrame);

		delete packet;

		av_packet_unref(originalPacket);
		delete originalPacket;

		if (conversionContext) {
			sws_freeContext(conversionContext);
		}

		if (decoderContext) {
			avcodec_close(decoderContext);
		}

		AVFormatContext *formatContext = this->formatContext;
		if (formatContext) {
			AVIOContext *inputContext = formatContext->pb;
			if (inputContext) {
				delete inputContext->opaque;
				av_freep(&inputContext->buffer);
				av_freep(&inputContext);
			}

			avformat_close_input(&formatContext);
		}
	}

	void Video::Seek(Int64 position)
	{
		if (avformat_seek_file(formatContext, videoStreamIndex, position, position, position, AVSEEK_FLAG_FRAME) < 0)
		{
			Debug::WriteLine("Error seeking to position");
		}
	}

	bool Video::AdvanceFrame(array<int>^ pixelData)
	{
		int gotFrame;
		int bytesDecoded;

		while (true) {
			switch (decodeState) {
			case ReadFrame:
				if (av_read_frame(formatContext, packet) >= 0) {
					*originalPacket = *packet;
					decodeState = DecodeNonCached;
				} else {
					decodeState = DecodeCached;
					continue;
				}
			case DecodeNonCached:
				bytesDecoded = DecodePacket(packet, frame, &gotFrame, 0, pixelData);
				if (bytesDecoded < 0) {
					decodeState = ReadFrame;
					continue;
				} else {
					packet->data += bytesDecoded;
					packet->size -= bytesDecoded;

					if (packet->size <= 0) {
						av_packet_unref(originalPacket);
						decodeState = ReadFrame;
					}
				}

				return true;
			case DecodeCached:
				// flush cached frames
				packet->data = NULL;
				packet->size = 0;
				DecodePacket(packet, frame, &gotFrame, 1, pixelData);

				if (!gotFrame) {
					decodeState = FinishedDecoding;
					return false;
				}

				return true;
			default:
				return false;
			}
		}
	}

	int Video::DecodePacket(AVPacket *packet, AVFrame *frame, int *gotFrame, int cached, array<int>^ pixelData)
	{
		int ret = 0;
		int decoded = packet->size;

		*gotFrame = 0;

		if (packet->stream_index == videoStreamIndex) {
			/* decode video frame */
			ret = avcodec_decode_video2(decoderContext, frame, gotFrame, packet);
			if (ret < 0) {
				Debug::WriteLine("Error decoding video frame");
				return ret;
			}

			if (*gotFrame) {
				int sourceWidth = decoderContext->width;
				int sourceHeight = decoderContext->height;

				conversionContext = sws_getCachedContext(conversionContext,
					sourceWidth, sourceHeight, decoderContext->pix_fmt,
					width, height, outputPixelFormat,
					SWS_BICUBIC, NULL, NULL, NULL);

				if (!conversionContext) {
					throw gcnew Exception("Could not initialize the conversion context");
				}

				sws_scale(conversionContext,
					(const uint8_t * const *)frame->data, frame->linesize,
					0, sourceHeight, tempFrame->data, tempFrame->linesize);

				// Copy each line in the frame. We can't copy the whole frame at once because the stride lengths may not match up.
				for (int y = 0; y < height; y++) {
					Marshal::Copy(IntPtr(tempFrame->data[0] + y * tempFrame->linesize[0]), pixelData, y * width, width);
				}
			}
		}

		return decoded;
	}
}
