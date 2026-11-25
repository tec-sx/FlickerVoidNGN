/*
 * The engine's DLSS library: a C API over NVIDIA's DLSS SDK (its NGX static library) for the engine's Vulkan device, which loads
 * the library only when it sits beside the program. Every call returns an NVSDK_NGX_Result: 1 on success, 0xBADxxxxx on failure.
 * The library serializes its calls, as NGX is not thread safe.
 */
#pragma once

#include <stdint.h>
#include <vulkan/vulkan.h>

#if defined(_WIN32)
#define FV_DLSS_API __declspec(dllexport)
#else
#define FV_DLSS_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* Who runs DLSS: NGX's project id (a GUID), the engine's version, a folder NGX may write its logs to and the folder holding
   NVIDIA's DLSS runtime, all UTF-8. */
typedef struct FvDlssApplication
{
    const char* ProjectId;
    const char* EngineVersion;
    const char* DataPath;
    const char* RuntimePath;
} FvDlssApplication;

typedef enum FvDlssMode
{
    FvDlssModeQuality = 1,
    FvDlssModeDlaa = 2,
} FvDlssMode;

/* An image DLSS reads or writes, by its Vulkan handles. */
typedef struct FvDlssImage
{
    uint64_t Image;
    uint64_t View;
    uint32_t Format;
    uint32_t Aspect;
    uint32_t Width;
    uint32_t Height;
} FvDlssImage;

/* A frame to upscale: HDR color, depth from 0 (near) to 1 (far), motion in render pixels towards the previous frame without the
   jitter, a 1x1 image holding the exposure the tonemapper multiplies by, and the output, which DLSS writes as a storage image.
   The jitter is in render pixels, right and down. */
typedef struct FvDlssFrame
{
    FvDlssImage Color;
    FvDlssImage Depth;
    FvDlssImage Motion;
    FvDlssImage Exposure;
    FvDlssImage Output;
    float JitterX;
    float JitterY;
    uint32_t RenderWidth;
    uint32_t RenderHeight;
    int32_t Reset;
} FvDlssFrame;

typedef struct FvDlssContext FvDlssContext;
typedef struct FvDlssFeature FvDlssFeature;

/* The instance extensions DLSS needs, before the instance is made; NGX owns the array. */
FV_DLSS_API uint32_t FvDlssInstanceExtensions(const FvDlssApplication* application, uint32_t* count, VkExtensionProperties** extensions);

/* Whether DLSS runs on the physical device, and the device extensions it then needs, before the device is made. */
FV_DLSS_API uint32_t FvDlssDeviceExtensions(const FvDlssApplication* application, VkInstance instance, VkPhysicalDevice physicalDevice, uint32_t* count, VkExtensionProperties** extensions);

/* Starts NGX on the device and checks DLSS is available there. */
FV_DLSS_API uint32_t FvDlssInit(const FvDlssApplication* application, VkInstance instance, VkPhysicalDevice physicalDevice, VkDevice device, PFN_vkGetInstanceProcAddr getInstanceProcAddr, PFN_vkGetDeviceProcAddr getDeviceProcAddr, FvDlssContext** context);

/* Stops NGX on the context's device, after its features are released. */
FV_DLSS_API uint32_t FvDlssShutdown(FvDlssContext* context);

/* The size a mode draws the scene at for an output's. */
FV_DLSS_API uint32_t FvDlssRenderSize(FvDlssContext* context, FvDlssMode mode, uint32_t outputWidth, uint32_t outputHeight, uint32_t* renderWidth, uint32_t* renderHeight);

/* Records making a DLSS feature into the command buffer, which must run before the feature evaluates. */
FV_DLSS_API uint32_t FvDlssCreate(FvDlssContext* context, VkCommandBuffer commands, FvDlssMode mode, uint32_t renderWidth, uint32_t renderHeight, uint32_t outputWidth, uint32_t outputHeight, FvDlssFeature** feature);

/* Records upscaling a frame; NGX leaves the command buffer's bound pipeline and descriptor sets undefined. */
FV_DLSS_API uint32_t FvDlssEvaluate(FvDlssContext* context, VkCommandBuffer commands, FvDlssFeature* feature, const FvDlssFrame* frame);

/* Releases a feature once the GPU has finished with it. */
FV_DLSS_API uint32_t FvDlssRelease(FvDlssFeature* feature);

#ifdef __cplusplus
}
#endif
