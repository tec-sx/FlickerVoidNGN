// Draws a mesh part whose vertices (float3 position, float3 normal, float2 uv) are pulled from a bindless buffer: its base
// color times its texture, lit by the sun, an even ambient light, and the point and spot lights of the pixel's cluster,
// shadowed through the shadow atlas and tinted by spot light cookies, plus bounced light from irradiance probes, seen
// through fog lit by the same lights.
// The frame buffer holds FrameData, then the lights, the shadows, each cluster's range of light indices, and the indices.

Texture2D Textures[] : register(t0, space1);
ByteAddressBuffer Buffers[] : register(t0, space2);
SamplerState Samplers[] : register(s0, space3);

struct SceneConstants
{
    row_major float4x4 World;
    float4 BaseColor;
    uint Frame;
    uint Vertices;
    uint Texture;
    uint Sampler;
};

struct FrameData
{
    float4x4 ViewProjection;
    float4 DepthAxis;
    float3 SunDirection;
    float Ambient;
    float2 TargetSize;
    float SliceScale;
    float SliceBias;
    uint LogarithmicSlices;
    uint LightCount;
    uint ClustersOffset;
    uint IndicesOffset;
    uint ShadowsOffset;
    uint SunShadow;
    uint ShadowAtlas;
    uint ClampSampler;
    float3 ProbeMin;
    uint Probes;
    float3 ProbeSize;
    uint3 ProbeCounts;
    float3 CameraPosition;
    float NearPlane;
    float3 FogColor;
    float FogDensity;
    float FogAnisotropy;
    float FogHeight;
    float FogHeightFalloff;
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
static const uint FrameDataSize = 240;
static const uint FogSteps = 16;
static const float Pi = 3.14159265;
static const uint ProbeSize = 48;
static const uint LightSize = 80;
static const uint ShadowSize = 96;
static const uint None = 0xFFFFFFFF;
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
};

FrameData LoadFrame()
{
    ByteAddressBuffer buffer = Buffers[Scene.Frame];
    FrameData frame;
    // Rows of a System.Numerics matrix, which multiplies row vectors.
    frame.ViewProjection = float4x4(buffer.Load<float4>(0), buffer.Load<float4>(16), buffer.Load<float4>(32), buffer.Load<float4>(48));
    frame.DepthAxis = buffer.Load<float4>(64);
    frame.SunDirection = buffer.Load<float3>(80);
    frame.Ambient = buffer.Load<float>(92);
    frame.TargetSize = buffer.Load<float2>(96);
    frame.SliceScale = buffer.Load<float>(104);
    frame.SliceBias = buffer.Load<float>(108);
    frame.LogarithmicSlices = buffer.Load<uint>(112);
    frame.LightCount = buffer.Load<uint>(116);
    frame.ClustersOffset = buffer.Load<uint>(120);
    frame.IndicesOffset = buffer.Load<uint>(124);
    frame.ShadowsOffset = buffer.Load<uint>(128);
    frame.SunShadow = buffer.Load<uint>(132);
    frame.ShadowAtlas = buffer.Load<uint>(136);
    frame.ClampSampler = buffer.Load<uint>(140);
    frame.ProbeMin = buffer.Load<float3>(144);
    frame.Probes = buffer.Load<uint>(156);
    frame.ProbeSize = buffer.Load<float3>(160);
    frame.ProbeCounts = buffer.Load<uint3>(172);
    frame.CameraPosition = buffer.Load<float3>(192);
    frame.NearPlane = buffer.Load<float>(204);
    frame.FogColor = buffer.Load<float3>(208);
    frame.FogDensity = buffer.Load<float>(220);
    frame.FogAnisotropy = buffer.Load<float>(224);
    frame.FogHeight = buffer.Load<float>(228);
    frame.FogHeightFalloff = buffer.Load<float>(232);
    return frame;
}

VertexOutput VertexMain(uint vertexId : SV_VertexID)
{
    FrameData frame = LoadFrame();
    ByteAddressBuffer vertices = Buffers[Scene.Vertices];
    uint offset = vertexId * VertexStride;
    float3 position = vertices.Load<float3>(offset);
    float3 normal = vertices.Load<float3>(offset + 12);
    float4 world = mul(float4(position, 1), Scene.World);

    // The cofactor matrix keeps normals at right angles to surfaces under non-uniform scale; mirroring flips its sign.
    float3x3 basis = (float3x3)Scene.World;
    float3x3 cofactor = float3x3(cross(basis[1], basis[2]), cross(basis[2], basis[0]), cross(basis[0], basis[1]));
    float mirror = sign(dot(basis[0], cofactor[0]));

    VertexOutput output;
    output.Position = mul(world, frame.ViewProjection);
    output.WorldPosition = world.xyz;
    output.Normal = mul(normal, cofactor) * mirror;
    output.Uv = vertices.Load<float2>(offset + 24);
    return output;
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

float3 ShadeLight(FrameData frame, Light light, float3 worldPosition, float3 normal)
{
    float3 direction;
    float3 incoming = IncomingLight(frame, light, worldPosition, normal, true, direction);
    return incoming * saturate(dot(normal, direction));
}

float3 ShadeClusterLights(FrameData frame, float2 pixel, float3 worldPosition, float3 normal)
{
    ByteAddressBuffer buffer = Buffers[Scene.Frame];
    uint cluster = FindCluster(frame, pixel, worldPosition);
    uint2 range = buffer.Load<uint2>(frame.ClustersOffset + cluster * 8);
    float3 light = 0;
    for (uint index = 0; index < range.y; index++)
    {
        uint lightIndex = buffer.Load<uint>(frame.IndicesOffset + (range.x + index) * 4);
        Light clusterLight = buffer.Load<Light>(FrameDataSize + lightIndex * LightSize);
        light += ShadeLight(frame, clusterLight, worldPosition, normal);
    }

    return light;
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
    float3 light = frame.Ambient + SampleProbes(frame, position, 0);
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

float4 PixelMain(VertexOutput input) : SV_Target
{
    FrameData frame = LoadFrame();
    float4 albedo = Textures[Scene.Texture].Sample(Samplers[Scene.Sampler], input.Uv) * Scene.BaseColor;
    float3 normal = normalize(input.Normal);
    float sun = saturate(dot(normal, -normalize(frame.SunDirection)));
    if (sun > 0 && frame.SunShadow != None)
    {
        sun *= SampleShadow(frame, frame.SunShadow, input.WorldPosition, normal, 0, true);
    }

    float3 light = frame.Ambient + (1 - frame.Ambient) * sun;
    light += SampleProbes(frame, input.WorldPosition, normal);
    light += ShadeClusterLights(frame, input.Position.xy, input.WorldPosition, normal);
    float3 color = albedo.rgb * light;
    if (frame.FogDensity > 0)
    {
        float4 fog = MarchFog(frame, input.Position.xy, input.WorldPosition);
        color = color * fog.a + fog.rgb;
    }

    return float4(color, albedo.a);
}
