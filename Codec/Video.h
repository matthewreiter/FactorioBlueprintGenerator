// Video.h

#pragma once

using namespace System;
using namespace System::IO;

namespace Codec
{
	private enum DecodeState
	{
		ReadFrame,
		DecodeNonCached,
		DecodeCached,
		FinishedDecoding
	};

	public ref class Video
	{
	public:
		Video(MemoryStream ^videoBuffer, int width, int height);
		~Video();
		!Video();

		void Seek(Int64 position);
		bool AdvanceFrame(array<int>^ pixelData);

		property double TimeBase {
			double get() { return timeBase; }
		}

		property Int64 Duration {
			Int64 get() { return duration; }
		}

		property Int64 Position {
			Int64 get() { return frame ? av_frame_get_best_effort_timestamp(frame) : 0; }
		}
	private:
		MemoryStream ^videoBuffer;
		AVFormatContext *formatContext;
		AVCodecContext *decoderContext;
		SwsContext *conversionContext;
		int videoStreamIndex;
		double timeBase;
		Int64 duration;
		AVFrame *frame;
		AVFrame *tempFrame;
		AVPacket *packet = new AVPacket();
		AVPacket *originalPacket = new AVPacket();
		DecodeState decodeState = ReadFrame;
		int width;
		int height;

		int DecodePacket(AVPacket *packet, AVFrame *frame, int *gotFrame, int cached, array<int>^ pixelData);
	};
}
