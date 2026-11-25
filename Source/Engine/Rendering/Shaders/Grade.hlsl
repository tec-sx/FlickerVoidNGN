// The mood's grade, which Post.hlsl lays on the lit scene: exposure, contrast about middle grey, saturation and tint, then the
// tonemapper's curve into the screen's range. Ungrade turns it back: from a colour meant for the screen to the light that
// grades to it, so passes that paint into the lit scene before the grade (the sky, the depth fade's far colour) look on screen
// as they were picked.

#pragma once

struct Grading
{
    float3 Tint;
    float Exposure;
    float Contrast;
    float Saturation;
    uint Tonemapper;
    float WhitePoint;
};

// Linear middle grey, which contrast pivots on.
static const float MiddleGrey = 0.18;

static const float3 ToLuminance = float3(0.2126, 0.7152, 0.0722);

// The Tonemapper enum's values.
static const uint TonemapUncharted2 = 1;
static const uint TonemapAces = 2;

// John Hable's filmic curve from Uncharted 2, with his shoulder, linear and toe constants.
static const float HableA = 0.15;
static const float HableB = 0.50;
static const float HableC = 0.10;
static const float HableD = 0.20;
static const float HableE = 0.02;
static const float HableF = 0.30;

float3 Hable(float3 x)
{
    return ((x * (HableA * x + HableC * HableB) + HableD * HableE) / (x * (HableA * x + HableB) + HableD * HableF)) - HableE / HableF;
}

// Where Hable's curve reaches the value: the positive root of A(1-k)x² + B(C-k)x + D(E-kF) = 0, with k the value plus E/F.
float3 InverseHable(float3 value)
{
    float3 k = value + HableE / HableF;
    float3 a = HableA * (1 - k);
    float3 b = HableB * (HableC - k);
    float3 c = HableD * (HableE - k * HableF);
    return (-b + sqrt(max(b * b - 4 * a * c, 0))) / (2 * max(a, 1e-6));
}

// Krzysztof Narkowicz's fit of the ACES film curve.
float3 Aces(float3 x)
{
    return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
}

// Where the ACES fit reaches the value: the root of (2.43v - 2.51)x² + (0.59v - 0.03)x + 0.14v = 0 its curve passes through.
float3 InverseAces(float3 value)
{
    float3 a = 2.43 * value - 2.51;
    float3 b = 0.59 * value - 0.03;
    float3 c = 0.14 * value;
    return (-b - sqrt(max(b * b - 4 * a * c, 0))) / (2 * a);
}

// Brings the graded light into the screen's range by the mood's tonemapper; Hable's exposure bias of 2 keeps middle grey
// where the untonemapped image has it.
float3 Tonemap(Grading grading, float3 color)
{
    switch (grading.Tonemapper)
    {
        case TonemapUncharted2:
            return Hable(color * 2) / Hable(grading.WhitePoint);
        case TonemapAces:
            return Aces(color);
        default:
            return color;
    }
}

float3 Untonemap(Grading grading, float3 color)
{
    if (grading.Tonemapper == TonemapUncharted2)
    {
        float3 white = Hable(grading.WhitePoint);
        return InverseHable(min(color, 0.999) * white) / 2;
    }

    if (grading.Tonemapper == TonemapAces)
    {
        return InverseAces(min(color, 0.999));
    }

    return color;
}

float3 Grade(Grading grading, float3 color)
{
    color *= grading.Exposure;
    color = pow(max(color, 0) / MiddleGrey, grading.Contrast) * MiddleGrey;
    float luminance = dot(color, ToLuminance);
    color = lerp(luminance, color, grading.Saturation);
    color *= grading.Tint;
    return Tonemap(grading, color);
}

// The light the grade turns into the screen colour, each step undone in turn; saturation keeps luminance, so it is undone
// about the colour's own.
float3 Ungrade(Grading grading, float3 screen)
{
    float3 color = Untonemap(grading, saturate(screen));
    color /= max(grading.Tint, 1e-4);
    float luminance = dot(color, ToLuminance);
    color = max(luminance + (color - luminance) / max(grading.Saturation, 1e-4), 0);
    color = pow(color / MiddleGrey, 1 / max(grading.Contrast, 1e-4)) * MiddleGrey;
    return color / max(grading.Exposure, 1e-4);
}
