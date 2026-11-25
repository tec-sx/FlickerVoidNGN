// Draws a mesh part whose vertices (float3 position, float3 normal, float2 uv) are pulled from a bindless buffer, as its record
// among the frame buffer's draws says (pushed by its number). The geometry pass leaves its surface in the G-buffer (GBuffer.cs):
// its base color times its texture and its material's tint, its normal, its material, its object's depth and its motion. The lit
// pass draws that surface shaded by the material's controls (Scene.fvshader) and lit by the sun, an even ambient light, and the
// point and spot lights of the pixel's cluster, shadowed through the shadow atlas and tinted by spot light cookies, plus bounced
// light from irradiance probes, seen through fog lit by the same lights. Indoor volumes scale the ambient light inside them, and
// while the camera looks into one (the cutaway) whatever stands in front of its box is cut away and the world outside it muted.
// The frame buffer holds FrameData (Frame.hlsl), then the lights, the shadows, each cluster's range of light indices, the
// indices, the indoor volumes and the draws. ScreenShadows.hlsl gives each pixel its ambient occlusion and contact shadow.

#include "Frame.hlsl"

struct SceneConstants
{
    uint Frame;
    uint Draw;
};

// A draw's record (GpuDraw.cs): where its mesh stands this frame and the frame before, its vertices then and now, its looks,
// its outline's color, its MeshFlags, and the centre of its box with how wholly it takes that depth for the depth tone.
struct Draw
{
    float4x4 World;
    float4x4 PreviousWorld;
    float4 BaseColor;
    float4 OutlineColor;
    uint Vertices;
    uint PreviousVertices;
    uint Texture;
    uint Sampler;
    uint Material;
    uint Flags;
    float3 Center;
    float Wholeness;
};

static const uint DrawSize = 208;

// MeshFlags.Uncut: the cutaway keeps the mesh, such as a door.
static const uint FlagUncut = 4;

// MeshFlags.Greyed: the mesh is drawn black and white, as the editor's creator greys the rest of a level.
static const uint FlagGreyed = 8;

// The parameters Scene.fvshader declares, in its order.
struct Material
{
    float3 Tint;
    float RampSoftness;
    float Wrap;
    float3 ShadowTint;
    float3 RimColor;
    float RimStrength;
    float RimPower;
    float Specular;
    float Glossiness;
    float3 Emissive;
    float EmissiveIntensity;
    uint Texture;
    uint Relief;
    float TextureScale;
    float ReliefDepth;
};

struct Light
{
    float3 Position;
    float Range;
    float3 Color;
    float CosOuter;
    float3 Direction;
    float CosInner;
    float3 Right;
    uint Cookie;
    uint Shadow;
};

struct Shadow
{
    float4x4 WorldToAtlas;
    float4 Rect;
    float TexelPerDistance;
    float TexelConstant;
};

[[vk::push_constant]] ConstantBuffer<SceneConstants> Scene : register(b0, space0);

static const uint VertexStride = 32;
static const uint FogSteps = 16;
static const float Pi = 3.14159265;
static const uint ProbeSize = 48;
static const uint LightSize = 80;
static const uint ShadowSize = 96;
static const float AtlasSize = 4096;

// Normal offset in texels and depth bias that keep surfaces from shadowing themselves.
static const float NormalBias = 1.5;
static const float DepthBias = 0.0003;
// The cluster grid of LightClusters.
static const uint TilesX = 16;
static const uint TilesY = 9;
static const uint Slices = 24;

struct VertexOutput
{
    float4 Position : SV_Position;
    float3 WorldPosition : POSITION;
    float3 Normal : NORMAL;
    float2 Uv : TEXCOORD0;
    // Where the vertex stood in the frame before, as the view then saw it.
    float4 PreviousClip : TEXCOORD1;
};

// The draw's record; its matrices are the rows of System.Numerics matrices, which multiply row vectors.
Draw LoadDraw(FrameData frame, uint index)
{
    ByteAddressBuffer buffer = Buffers[frame.Buffer];
    uint offset = frame.DrawsOffset + index * DrawSize;
    Draw draw;
    draw.World = float4x4(buffer.Load<float4>(offset), buffer.Load<float4>(offset + 16), buffer.Load<float4>(offset + 32), buffer.Load<float4>(offset + 48));
    draw.PreviousWorld = float4x4(buffer.Load<float4>(offset + 64), buffer.Load<float4>(offset + 80), buffer.Load<float4>(offset + 96), buffer.Load<float4>(offset + 112));
    draw.BaseColor = buffer.Load<float4>(offset + 128);
    draw.OutlineColor = buffer.Load<float4>(offset + 144);
    draw.Vertices = buffer.Load<uint>(offset + 160);
    draw.PreviousVertices = buffer.Load<uint>(offset + 164);
    draw.Texture = buffer.Load<uint>(offset + 168);
    draw.Sampler = buffer.Load<uint>(offset + 172);
    draw.Material = buffer.Load<uint>(offset + 176);
    draw.Flags = buffer.Load<uint>(offset + 180);
    draw.Center = buffer.Load<float3>(offset + 192);
    draw.Wholeness = buffer.Load<float>(offset + 204);
    return draw;
}

VertexOutput VertexMain(uint vertexId : SV_VertexID)
{
    FrameData frame = LoadFrame(Scene.Frame);
    Draw draw = LoadDraw(frame, Scene.Draw);
    ByteAddressBuffer vertices = Buffers[draw.Vertices];
    uint offset = vertexId * VertexStride;
    float3 position = vertices.Load<float3>(offset);
    float3 normal = vertices.Load<float3>(offset + 12);
    float4 world = mul(float4(position, 1), draw.World);

    // The cofactor matrix keeps normals at right angles to surfaces under non-uniform scale; mirroring flips its sign.
    float3x3 basis = (float3x3)draw.World;
    float3x3 cofactor = float3x3(cross(basis[1], basis[2]), cross(basis[2], basis[0]), cross(basis[0], basis[1]));
    float mirror = sign(dot(basis[0], cofactor[0]));

    VertexOutput output;
    output.Position = mul(world, frame.ViewProjection);
    output.WorldPosition = world.xyz;
    output.Normal = mul(normal, cofactor) * mirror;
    output.Uv = vertices.Load<float2>(offset + 24);
    float3 previousPosition = Buffers[draw.PreviousVertices].Load<float3>(offset);
    float4 previousWorld = mul(float4(previousPosition, 1), draw.PreviousWorld);
    output.PreviousClip = mul(previousWorld, frame.PreviousViewProjection);
    return output;
}

// The material's parameters, packed tightly in declared order by MaterialLayout.
Material LoadMaterial(uint index)
{
    ByteAddressBuffer buffer = Buffers[index];
    Material material;
    material.Tint = buffer.Load<float3>(0);
    material.RampSoftness = buffer.Load<float>(12);
    material.Wrap = buffer.Load<float>(16);
    material.ShadowTint = buffer.Load<float3>(20);
    material.RimColor = buffer.Load<float3>(32);
    material.RimStrength = buffer.Load<float>(44);
    material.RimPower = buffer.Load<float>(48);
    material.Specular = buffer.Load<float>(52);
    material.Glossiness = buffer.Load<float>(56);
    material.Emissive = buffer.Load<float3>(60);
    material.EmissiveIntensity = buffer.Load<float>(72);
    material.Texture = buffer.Load<uint>(76);
    material.Relief = buffer.Load<uint>(80);
    material.TextureScale = buffer.Load<float>(84);
    material.ReliefDepth = buffer.Load<float>(88);
    return material;
}

// The share of light a surface takes at the cosine between its normal and the light: wrapped round past the terminator,
// then pulled toward a hard step as the ramp softness falls (1 is plain wrapped diffuse light).
float Diffuse(Material material, float cosine)
{
    float wrapped = saturate((cosine + material.Wrap) / (1 + material.Wrap));
    float halfWidth = max(0.5 * material.RampSoftness, 0.001);
    float stepped = smoothstep(0.5 - halfWidth, 0.5 + halfWidth, wrapped);
    return lerp(stepped, wrapped, material.RampSoftness);
}

// A Blinn-Phong highlight, sharper as the glossiness rises, on surfaces facing the light.
float Specular(Material material, float3 normal, float3 toLight, float3 toCamera)
{
    if (material.Specular <= 0 || dot(normal, toLight) <= 0)
    {
        return 0;
    }

    float3 halfway = normalize(toLight + toCamera);
    float exponent = exp2(1 + material.Glossiness * 10);
    return material.Specular * pow(saturate(dot(normal, halfway)), exponent);
}

// The direction from the point to the camera: toward its eye in perspective, against its view in orthographic.
float3 ToCamera(FrameData frame, float3 worldPosition)
{
    if (frame.LogarithmicSlices != 0)
    {
        return normalize(frame.CameraPosition - worldPosition);
    }

    return -normalize(frame.DepthAxis.xyz);
}

// Indoor volumes reach a little past their boxes, so walls, floors and ceilings standing on them count as inside.
static const float IndoorMargin = 0.05;
static const uint IndoorSize = 32;

bool IsInBox(float3 position, float3 boxMin, float3 boxMax)
{
    return all(position >= boxMin - IndoorMargin) && all(position <= boxMax + IndoorMargin);
}

// The share of the mood's ambient light at a point: the least of the indoor volumes it stands in, else all of it.
float IndoorAmbient(FrameData frame, float3 worldPosition)
{
    ByteAddressBuffer buffer = Buffers[Scene.Frame];
    float ambient = 1;
    for (uint index = 0; index < frame.IndoorCount; index++)
    {
        uint offset = frame.IndoorsOffset + index * IndoorSize;
        float3 boxMin = buffer.Load<float3>(offset);
        float boxAmbient = buffer.Load<float>(offset + 12);
        float3 boxMax = buffer.Load<float3>(offset + 16);
        if (IsInBox(worldPosition, boxMin, boxMax))
        {
            ambient = min(ambient, boxAmbient);
        }
    }

    return ambient;
}

// Whether a point stands between the camera and the cutaway's box: outside it, with the box behind it along the view.
bool IsCutAway(FrameData frame, float3 worldPosition)
{
    if (frame.Cut == 0 || IsInBox(worldPosition, frame.CutMin, frame.CutMax))
    {
        return false;
    }

    float3 away = -ToCamera(frame, worldPosition);
    away = select(abs(away) < 1e-6, 1e-6, away);
    float3 toMin = (frame.CutMin - IndoorMargin - worldPosition) / away;
    float3 toMax = (frame.CutMax + IndoorMargin - worldPosition) / away;
    float3 nearSides = min(toMin, toMax);
    float3 farSides = max(toMin, toMax);
    float enter = max(max(nearSides.x, nearSides.y), max(nearSides.z, 0));
    float leave = min(farSides.x, min(farSides.y, farSides.z));
    return enter <= leave;
}

// Greys and darkens the world outside the cutaway's box by its muting, so the room it looks into stands out.
float3 MuteOutside(FrameData frame, float3 color, float3 worldPosition)
{
    if (frame.Cut == 0 || frame.CutMuted <= 0 || IsInBox(worldPosition, frame.CutMin, frame.CutMax))
    {
        return color;
    }

    float grey = dot(color, float3(0.2126, 0.7152, 0.0722));
    float3 muted = lerp(color, grey, frame.CutMuted);
    return muted * (1 - (0.5 * frame.CutMuted));
}

// The point light shadow face whose view holds the offset from the light, by its largest axis: +X, -X, +Y, -Y, +Z, -Z.
uint PointFace(float3 offset)
{
    float3 size = abs(offset);
    if (size.x >= size.y && size.x >= size.z)
    {
        return offset.x >= 0 ? 0 : 1;
    }

    if (size.y >= size.z)
    {
        return offset.y >= 0 ? 2 : 3;
    }

    return offset.z >= 0 ? 4 : 5;
}

uint FindCluster(FrameData frame, float2 pixel, float3 worldPosition)
{
    uint2 tile = min(uint2(pixel * float2(TilesX, TilesY) / frame.TargetSize), uint2(TilesX - 1, TilesY - 1));
    float depth = dot(float4(worldPosition, 1), frame.DepthAxis);
    if (frame.LogarithmicSlices != 0)
    {
        depth = log(max(depth, 1e-6));
    }

    float slice = clamp(floor(depth * frame.SliceScale + frame.SliceBias), 0, Slices - 1);
    return ((uint)slice * TilesY + tile.y) * TilesX + tile.x;
}

Shadow LoadShadow(FrameData frame, uint index)
{
    ByteAddressBuffer buffer = Buffers[Scene.Frame];
    uint offset = frame.ShadowsOffset + index * ShadowSize;
    Shadow shadow;
    shadow.WorldToAtlas = float4x4(buffer.Load<float4>(offset), buffer.Load<float4>(offset + 16), buffer.Load<float4>(offset + 32), buffer.Load<float4>(offset + 48));
    shadow.Rect = buffer.Load<float4>(offset + 64);
    shadow.TexelPerDistance = buffer.Load<float>(offset + 80);
    shadow.TexelConstant = buffer.Load<float>(offset + 84);
    return shadow;
}

// The share of four atlas texels around the point that the depth is in front of, blended by the point's place between them.
float ShadowTap(FrameData frame, float2 uv, float depth)
{
    float2 texel = uv * AtlasSize - 0.5;
    float2 blend = frac(texel);
    float2 gatherUv = (floor(texel) + 1) / AtlasSize;
    float4 depths = Textures[frame.ShadowAtlas].GatherRed(Samplers[frame.ClampSampler], gatherUv);
    // Gather returns texels (0, 1), (1, 1), (1, 0) and (0, 0), counting right and down.
    float4 lit = step(depth, depths);
    float top = lerp(lit.w, lit.z, blend.x);
    float bottom = lerp(lit.x, lit.y, blend.x);
    return lerp(top, bottom, blend.y);
}

// 1 where the shadow's view sees the point, 0 where something hides it, softened over a 3x3 texel filter, or over the
// four texels round it when not soft (fog takes many samples, each blurred by the march anyway).
float SampleShadow(FrameData frame, uint index, float3 worldPosition, float3 normal, float distance, bool soft)
{
    Shadow shadow = LoadShadow(frame, index);
    float texelSize = shadow.TexelPerDistance * distance + shadow.TexelConstant;
    float3 offsetPosition = worldPosition + normal * texelSize * NormalBias;
    float4 clip = mul(float4(offsetPosition, 1), shadow.WorldToAtlas);
    float3 atlas = clip.xyz / clip.w;
    bool insideTile = all(atlas.xy >= shadow.Rect.xy) && all(atlas.xy <= shadow.Rect.zw);
    if (!insideTile || atlas.z > 1)
    {
        return 1;
    }

    float depth = atlas.z - DepthBias;
    if (!soft)
    {
        return ShadowTap(frame, atlas.xy, depth);
    }

    float lit = 0;
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 uv = clamp(atlas.xy + float2(x, y) / AtlasSize, shadow.Rect.xy, shadow.Rect.zw);
            lit += ShadowTap(frame, uv, depth);
        }
    }

    return lit / 9;
}

// The cookie's color where the light's ray through the point crosses it, the outer cone filling the image.
float3 SampleCookie(FrameData frame, Light light, float3 fromLight)
{
    float3 up = cross(light.Right, light.Direction);
    float3 local = float3(dot(fromLight, light.Right), dot(fromLight, up), dot(fromLight, light.Direction));
    float tanOuter = sqrt(saturate(1 - light.CosOuter * light.CosOuter)) / light.CosOuter;
    float2 spread = local.xy / (local.z * tanOuter);
    float2 uv = float2(0.5 + 0.5 * spread.x, 0.5 - 0.5 * spread.y);
    // Level 0: the light loop isn't uniform across pixels, so there are no derivatives to pick a mip with.
    return Textures[light.Cookie].SampleLevel(Samplers[frame.ClampSampler], uv, 0).rgb;
}

// The light arriving at a point from the light, with inverse-square falloff softened near the light and windowed to reach
// zero at its range, the spot cone, the cookie and the shadow; direction points toward the light. A surface's normal
// offsets the shadow lookup, and a point in fog has none.
float3 IncomingLight(FrameData frame, Light light, float3 worldPosition, float3 normal, bool soft, out float3 direction)
{
    float3 toLight = light.Position - worldPosition;
    float distanceSquared = dot(toLight, toLight);
    float distance = sqrt(distanceSquared);
    direction = toLight / max(distance, 1e-4);
    float ratio = distanceSquared / (light.Range * light.Range);
    float window = saturate(1 - ratio * ratio);
    float falloff = window * window / (distanceSquared + 1);
    float cone = smoothstep(light.CosOuter, light.CosInner, dot(-direction, light.Direction));
    float3 color = light.Color * (falloff * cone);
    if (all(color <= 0))
    {
        return 0;
    }

    if (light.Cookie != None)
    {
        color *= SampleCookie(frame, light, -direction);
    }

    if (light.Shadow != None)
    {
        uint shadow = light.Shadow;
        if (light.CosOuter < -1)
        {
            shadow += PointFace(-toLight);
        }

        color *= SampleShadow(frame, shadow, worldPosition, normal, distance, soft);
    }

    return color;
}

// Adds the diffuse and specular light of the pixel's cluster lights.
void ShadeClusterLights(FrameData frame, Material material, float2 pixel, float3 worldPosition, float3 normal, float3 toCamera, inout float3 diffuse, inout float3 specular)
{
    ByteAddressBuffer buffer = Buffers[Scene.Frame];
    uint cluster = FindCluster(frame, pixel, worldPosition);
    uint2 range = buffer.Load<uint2>(frame.ClustersOffset + cluster * 8);
    for (uint index = 0; index < range.y; index++)
    {
        uint lightIndex = buffer.Load<uint>(frame.IndicesOffset + (range.x + index) * 4);
        Light clusterLight = buffer.Load<Light>(FrameDataSize + lightIndex * LightSize);
        float3 direction;
        float3 incoming = IncomingLight(frame, clusterLight, worldPosition, normal, true, direction);
        float cosine = dot(normal, direction);
        diffuse += incoming * Diffuse(material, cosine);
        specular += incoming * Specular(material, normal, direction, toCamera);
    }
}

// A probe's spherical harmonics turned into the irradiance (over pi) a surface facing the normal gets.
float3 EvaluateProbe(ByteAddressBuffer probes, uint index, float3 normal)
{
    uint offset = index * ProbeSize;
    float4 basis = float4(1, normal);
    float red = dot(probes.Load<float4>(offset), basis);
    float green = dot(probes.Load<float4>(offset + 16), basis);
    float blue = dot(probes.Load<float4>(offset + 32), basis);
    return max(float3(red, green, blue), 0);
}

// The irradiance blended from the eight probes round the point, or nothing outside the volume.
float3 SampleProbes(FrameData frame, float3 worldPosition, float3 normal)
{
    if (frame.Probes == None)
    {
        return 0;
    }

    float3 local = (worldPosition - frame.ProbeMin) / frame.ProbeSize;
    if (any(local < 0) || any(local > 1))
    {
        return 0;
    }

    ByteAddressBuffer probes = Buffers[frame.Probes];
    uint3 last = frame.ProbeCounts - 1;
    float3 grid = clamp(local * float3(frame.ProbeCounts) - 0.5, 0, float3(last));
    uint3 low = min(uint3(grid), last);
    uint3 high = min(low + 1, last);
    float3 blend = grid - float3(low);
    float3 irradiance = 0;
    for (uint corner = 0; corner < 8; corner++)
    {
        uint3 bits = uint3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);
        uint3 cell = low + bits * (high - low);
        float3 weights = lerp(1 - blend, blend, float3(bits));
        uint index = (cell.z * frame.ProbeCounts.y + cell.y) * frame.ProbeCounts.x + cell.x;
        irradiance += weights.x * weights.y * weights.z * EvaluateProbe(probes, index, normal);
    }

    return irradiance;
}

// Henyey-Greenstein phase, scaled so even scattering is 1, for light turning by the angle whose cosine is given.
float Phase(float cosAngle, float anisotropy)
{
    float g = anisotropy;
    float denominator = 1 + g * g - 2 * g * cosAngle;
    return (1 - g * g) / (denominator * sqrt(denominator));
}

// The light a point in fog scatters toward the camera, from every light that reaches it, before the fog's color.
float3 FogLight(FrameData frame, float2 pixel, float3 position, float3 towardCamera)
{
    float3 light = frame.Ambient * IndoorAmbient(frame, position) + SampleProbes(frame, position, 0);
    float3 sunDirection = normalize(frame.SunDirection);
    float sun = (1 - frame.Ambient) * Phase(dot(sunDirection, towardCamera), frame.FogAnisotropy);
    if (frame.SunShadow != None)
    {
        sun *= SampleShadow(frame, frame.SunShadow, position, 0, 0, false);
    }

    light += sun;
    ByteAddressBuffer buffer = Buffers[Scene.Frame];
    uint cluster = FindCluster(frame, pixel, position);
    uint2 range = buffer.Load<uint2>(frame.ClustersOffset + cluster * 8);
    for (uint index = 0; index < range.y; index++)
    {
        uint lightIndex = buffer.Load<uint>(frame.IndicesOffset + (range.x + index) * 4);
        Light clusterLight = buffer.Load<Light>(FrameDataSize + lightIndex * LightSize);
        float3 direction;
        float3 incoming = IncomingLight(frame, clusterLight, position, 0, false, direction);
        light += incoming * Phase(dot(-direction, towardCamera), frame.FogAnisotropy);
    }

    return light;
}

// A small per-pixel offset that spreads the march's banding into fine noise (interleaved gradient noise).
float PixelNoise(float2 pixel)
{
    return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
}

// Fog density at a point: full at and below the fog height, thinning exponentially above it.
float FogDensityAt(FrameData frame, float3 position)
{
    float above = max(position.y - frame.FogHeight, 0);
    return frame.FogDensity * exp(-above * frame.FogHeightFalloff);
}

// Marches from the near plane to the surface in even steps, integrating each step exactly for light and density that hold
// still across it. Returns the light the fog scatters toward the camera and the share of the surface's light it lets through.
float4 MarchFog(FrameData frame, float2 pixel, float3 surface)
{
    float3 forward = frame.DepthAxis.xyz;
    float3 direction = forward;
    if (frame.LogarithmicSlices != 0)
    {
        direction = normalize(surface - frame.CameraPosition);
    }

    float depth = dot(float4(surface, 1), frame.DepthAxis);
    float rayLength = max(depth - frame.NearPlane, 0) / dot(direction, forward);
    float stepLength = rayLength / FogSteps;
    float jitter = PixelNoise(pixel);
    float3 scattered = 0;
    float transmittance = 1;
    for (uint index = 0; index < FogSteps; index++)
    {
        float travelled = (index + jitter) * stepLength;
        float3 position = surface - direction * (rayLength - travelled);
        float stepTransmittance = exp(-FogDensityAt(frame, position) * stepLength);
        if (stepTransmittance >= 1)
        {
            continue;
        }

        float3 light = FogLight(frame, pixel, position, -direction);
        scattered += transmittance * light * (1 - stepTransmittance);
        transmittance *= stepTransmittance;
    }

    return float4(scattered * frame.FogColor, transmittance);
}

// The pixel's ambient occlusion averaged over the 3x3 pixels round it, which smooths out the noise that turns its
// samples, and its own contact shadow, which stays sharp.
float2 BlurScreenShadows(FrameData frame, float2 pixel)
{
    Texture2D screenShadows = Textures[frame.ScreenShadows];
    int2 last = int2(frame.TargetSize) - 1;
    float occlusion = 0;
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            int2 neighbour = clamp(int2(pixel) + int2(x, y), 0, last);
            occlusion += screenShadows.Load(int3(neighbour, 0)).r;
        }
    }

    float contact = screenShadows.Load(int3(pixel, 0)).g;
    return float2(occlusion / 9, contact);
}

// The outline pass draws outlined meshes' silhouettes in their outline's color.
float4 OutlineMain(VertexOutput input) : SV_Target
{
    FrameData frame = LoadFrame(Scene.Frame);
    Draw draw = LoadDraw(frame, Scene.Draw);
    return float4(draw.OutlineColor.rgb, 1);
}

// Tilts the normal by the slope of a painting's relief, so its impasto catches the light without tangents: the relief's
// slope across the uvs, turned into the screen's by the uvs' derivatives, bends the normal by Mikkelsen's surface gradient
// over the position's derivatives. The derivatives come from the caller, outside the branch that reads the relief. Gives how
// high the paint stands there too.
float RaiseRelief(Material material, SamplerState sampler, float2 uv, float2 uvX, float2 uvY, float3 positionX, float3 positionY, inout float3 normal)
{
    Texture2D relief = Textures[material.Relief];
    float width;
    float height;
    relief.GetDimensions(width, height);
    float2 size = float2(width, height);
    float2 texelsX = uvX * size;
    float2 texelsY = uvY * size;
    float level = max(0.5 * log2(max(dot(texelsX, texelsX), dot(texelsY, texelsY))), 0);
    float2 step = exp2(level) / size;
    float left = relief.SampleLevel(sampler, uv - float2(step.x, 0), level).r;
    float right = relief.SampleLevel(sampler, uv + float2(step.x, 0), level).r;
    float up = relief.SampleLevel(sampler, uv - float2(0, step.y), level).r;
    float down = relief.SampleLevel(sampler, uv + float2(0, step.y), level).r;
    float centre = relief.SampleLevel(sampler, uv, level).r;
    float2 slope = float2(right - left, down - up) / (2 * step) * material.ReliefDepth;
    float heightX = dot(slope, uvX);
    float heightY = dot(slope, uvY);
    float3 acrossY = cross(positionY, normal);
    float3 acrossX = cross(normal, positionX);
    float determinant = dot(positionX, acrossY);
    float3 gradient = sign(determinant) * (heightX * acrossY + heightY * acrossX);
    if (material.ReliefDepth > 0)
    {
        normal = normalize(abs(determinant) * normal - gradient);
    }

    return centre;
}

// The relief's height (a share of the painting's impasto range) at which paint stands thick enough to hide the canvas.
static const float ImpastoCover = 0.1;

// A draw's surface at a pixel before any light: its color and coverage, its normal tilted by a painting's relief, and how thick
// its paint stands (1 where the impasto hides the canvas).
struct Surface
{
    float4 Albedo;
    float3 Normal;
    float Impasto;
};

Surface ReadSurface(VertexOutput input, Draw draw, Material material)
{
    SamplerState sampler = Samplers[draw.Sampler];
    uint texture = draw.Texture;
    float2 uv = input.Uv;
    if (material.Texture != None)
    {
        texture = material.Texture;
        uv *= material.TextureScale;
    }

    Surface surface;
    surface.Albedo = Textures[texture].Sample(sampler, uv) * draw.BaseColor;
    surface.Albedo.rgb *= material.Tint;
    surface.Normal = normalize(input.Normal);
    float2 uvX = ddx(uv);
    float2 uvY = ddy(uv);
    float3 positionX = ddx(input.WorldPosition);
    float3 positionY = ddy(input.WorldPosition);
    surface.Impasto = 0;
    if (material.Texture != None && material.Relief != None)
    {
        float height = RaiseRelief(material, sampler, uv, uvX, uvY, positionX, positionY, surface.Normal);
        surface.Impasto = saturate(height / ImpastoCover);
    }

    return surface;
}

// Whether the cutaway cuts the draw away at a point: anything in front of its box but the meshes it keeps.
bool IsCut(FrameData frame, Draw draw, float3 worldPosition)
{
    return (draw.Flags & FlagUncut) == 0 && IsCutAway(frame, worldPosition);
}

// A unit vector folded onto an octahedron's square, so two channels hold it (Cigolle et al. 2014, "A Survey of Efficient
// Representations for Independent Unit Vectors").
float2 EncodeNormal(float3 normal)
{
    float2 folded = normal.xy / (abs(normal.x) + abs(normal.y) + abs(normal.z));
    if (normal.z < 0)
    {
        float2 signs = select(folded >= 0, float2(1, 1), float2(-1, -1));
        folded = (1 - abs(folded.yx)) * signs;
    }

    return folded;
}

// The pixel (with fraction) a clip position lands on.
float2 ClipToPixel(FrameData frame, float4 clip)
{
    float2 ndc = clip.xy / clip.w;
    return float2(ndc.x * 0.5 + 0.5, 0.5 - ndc.y * 0.5) * frame.TargetSize;
}

// The G-buffer's targets, in GBuffer.Formats' order.
struct GeometryOutput
{
    float4 Albedo : SV_Target0;
    float2 Normal : SV_Target1;
    float4 Material : SV_Target2;
    float ObjectDepth : SV_Target3;
    float2 Motion : SV_Target4;
};

// The geometry pass leaves the surface for lighting, unlit. It runs VertexMain as the lit pass does, so their depths match
// exactly, and cuts away what the lit pass does. Its material target holds bytes: the material's bindless index in two, the
// paint's thickness and the mesh's flags. The object depth leans from the pixel's own view depth to its box centre's as the draw
// is small (Draw.Wholeness), and the motion is where the surface stood in the frame before less where it stands now, in pixels.
GeometryOutput GeometryMain(VertexOutput input)
{
    FrameData frame = LoadFrame(Scene.Frame);
    Draw draw = LoadDraw(frame, Scene.Draw);
    if (IsCut(frame, draw, input.WorldPosition))
    {
        discard;
    }

    Material material = LoadMaterial(draw.Material);
    Surface surface = ReadSurface(input, draw, material);
    GeometryOutput output;
    output.Albedo = surface.Albedo;
    output.Normal = EncodeNormal(surface.Normal);
    uint2 materialBytes = uint2(draw.Material & 0xFF, (draw.Material >> 8) & 0xFF);
    output.Material = float4(float2(materialBytes) / 255, surface.Impasto, float(draw.Flags & 0xFF) / 255);
    float depth = dot(float4(input.WorldPosition, 1), frame.DepthAxis);
    float centerDepth = dot(float4(draw.Center, 1), frame.DepthAxis);
    output.ObjectDepth = lerp(depth, centerDepth, draw.Wholeness);
    float2 previous = ClipToPixel(frame, input.PreviousClip);
    output.Motion = previous - input.Position.xy;
    return output;
}

float4 PixelMain(VertexOutput input) : SV_Target
{
    FrameData frame = LoadFrame(Scene.Frame);
    Draw draw = LoadDraw(frame, Scene.Draw);
    if (IsCut(frame, draw, input.WorldPosition))
    {
        discard;
    }

    Material material = LoadMaterial(draw.Material);
    Surface surface = ReadSurface(input, draw, material);
    float4 albedo = surface.Albedo;
    float3 normal = surface.Normal;
    float impasto = surface.Impasto;
    float3 toCamera = ToCamera(frame, input.WorldPosition);
    float2 screenShadows = BlurScreenShadows(frame, input.Position.xy);
    float3 toSun = -normalize(frame.SunDirection);
    float sunDiffuse = Diffuse(material, dot(normal, toSun));
    float sunSpecular = Specular(material, normal, toSun, toCamera);
    float sun = (1 - frame.Ambient) * screenShadows.g;
    if (sun > 0 && sunDiffuse + sunSpecular > 0 && frame.SunShadow != None)
    {
        sun *= SampleShadow(frame, frame.SunShadow, input.WorldPosition, normal, 0, true);
    }

    float3 ambient = frame.Ambient * IndoorAmbient(frame, input.WorldPosition) + SampleProbes(frame, input.WorldPosition, normal);
    float3 diffuse = ambient * screenShadows.r + sun * sunDiffuse;
    float3 specular = sun * sunSpecular;
    ShadeClusterLights(frame, material, input.Position.xy, input.WorldPosition, normal, toCamera, diffuse, specular);
    float3 color = albedo.rgb * diffuse + specular;

    // Painted touches: a tint where little light falls, a rim toward the silhouette, and the material's own glow.
    float lit = saturate(max(diffuse.r, max(diffuse.g, diffuse.b)));
    color += albedo.rgb * material.ShadowTint * (1 - lit);
    float facing = saturate(dot(normal, toCamera));
    color += material.RimColor * (material.RimStrength * pow(1 - facing, material.RimPower));
    color += material.Emissive * material.EmissiveIntensity;
    if (frame.FogDensity > 0)
    {
        float4 fog = MarchFog(frame, input.Position.xy, input.WorldPosition);
        color = color * fog.a + fog.rgb;
    }

    color = MuteOutside(frame, color, input.WorldPosition);
    if ((draw.Flags & FlagGreyed) != 0)
    {
        color = dot(color, float3(0.2126, 0.7152, 0.0722));
    }

    return float4(color, albedo.a + impasto * albedo.a);
}
