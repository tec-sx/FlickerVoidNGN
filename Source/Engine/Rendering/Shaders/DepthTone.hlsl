// The depth tone's ramps, shared by Lighting.hlsl (the near shadow) and DepthFade.hlsl (the burn and the far colour loss): an
// object's tone is read from its object depth (GBuffer.cs), the one depth a whole object stands at, measured along the view from
// the frame's focus.

#pragma once

#include "Frame.hlsl"

// 0 at or before start, 1 at or past end, eased between.
float Ramp(float start, float end, float value)
{
    float amount = saturate((value - start) / max(end - start, 1e-4));
    return amount * amount * (3 - 2 * amount);
}

// How far in front of the focus an object depth lies, negative behind it.
float BeforeFocus(FrameData frame, float objectDepth)
{
    return frame.FocusDepth - objectDepth;
}

// How much of the near amount an object depth takes, from 0 at the near start to 1 at its end.
float NearRamp(FrameData frame, float objectDepth)
{
    return Ramp(frame.NearStart, frame.NearEnd, BeforeFocus(frame, objectDepth));
}

// How much of the far amount an object depth takes, from 0 at the far start to 1 at its end.
float FarRamp(FrameData frame, float objectDepth)
{
    return Ramp(frame.FarStart, frame.FarEnd, -BeforeFocus(frame, objectDepth));
}

// The share of its direct light a near object keeps, as if a shadow fell on it; the ambient light is not touched.
float NearShadow(FrameData frame, float objectDepth)
{
    return 1 - frame.NearAmount * NearRamp(frame, objectDepth);
}
