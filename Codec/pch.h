// stdafx.h : include file for standard system include files,
// or project specific include files that are used frequently,
// but are changed infrequently

#pragma once

#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <vcclr.h>

#define snprintf _snprintf // timestamp.h uses snprintf instead of _snprintf

#pragma warning(push)
#pragma warning(disable: 4996) // Disable _CRT_SECURE_NO_WARNINGS for ffmpeg header files

extern "C" {
#include <libavcodec/avcodec.h>
#include <libavformat/avformat.h>
#include <libavutil/avassert.h>
#include <libavutil/channel_layout.h>
#include <libavutil/common.h>
#include <libavutil/imgutils.h>
#include <libavutil/mathematics.h>
#include <libavutil/opt.h>
#include <libavutil/samplefmt.h>
#include <libavutil/timestamp.h>
#include <libswresample/swresample.h>
#include <libswscale/swscale.h>
}

#pragma warning(pop)

#undef PixelFormat // Undefine PixelFormat because it clashes with System::Drawing::Imaging::PixelFormat
