#include "pch.h"

#include "Utils.h"

using namespace System;

AVFrame *AllocPicture(enum AVPixelFormat pixelFormat, int width, int height)
{
	AVFrame *picture = av_frame_alloc();
	if (!picture)
		throw gcnew Exception("Could not allocate video frame");

	picture->format = pixelFormat;
	picture->width = width;
	picture->height = height;

	// Allocate the buffers for the frame data
	if (av_frame_get_buffer(picture, 32) < 0) {
		throw gcnew Exception("Could not allocate video frame buffer");
	}

	return picture;
}
