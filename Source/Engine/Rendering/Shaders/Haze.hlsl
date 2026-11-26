// The haze, the cheap air beyond the view's focus: height fog in closed form, no marching. What lies behind the focus plane by
// more than the haze's start melts toward the haze's colour by 1 - exp(-density x the distance beyond the start x the thinning
// with height), up to its most. The colour is meant for the screen and is turned back through the mood's grade by the passes
// that lay it. The sky takes the same haze toward its horizon.

#pragma once

#include "Frame.hlsl"

// The sine of the height over the horizon at which the sky is clear of the haze.
static const float SkyHazeHeight = 0.25;

// How much of a surface's light the haze replaces at a world position: its depth along the view, less the focus and the haze's
// start, thickened by the density and thinned by the position's height above the haze's.
float HazeAmount(FrameData frame, float3 world)
{
    if (frame.HazeDensity <= 0)
    {
        return 0;
    }

    float beyond = max(dot(float4(world, 1), frame.DepthAxis) - frame.FocusDepth - frame.HazeStart, 0);
    float above = max(world.y - frame.HazeHeight, 0);
    float thickness = frame.HazeDensity * beyond * exp(-above * frame.HazeHeightFalloff);
    return frame.HazeMax * (1 - exp(-thickness));
}

// How much of the sky the haze replaces at a height over the horizon (the view ray's y): all of its most at and below the
// horizon, none from SkyHazeHeight up.
float SkyHazeAmount(FrameData frame, float elevation)
{
    if (frame.HazeDensity <= 0)
    {
        return 0;
    }

    return frame.HazeMax * (1 - smoothstep(0, SkyHazeHeight, elevation));
}
