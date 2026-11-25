#include "FlickerVoid.Dlss.h"

#include <filesystem>
#include <memory>
#include <mutex>
#include <string>

#include <nvsdk_ngx_helpers.h>
#include <nvsdk_ngx_helpers_vk.h>

namespace
{
    std::mutex Lock;

    std::wstring Wide(const char* utf8)
    {
        std::filesystem::path path = std::filesystem::u8path(utf8);
        return path.wstring();
    }

    // The application's strings as NGX takes them, and the feature discovery info pointing at them.
    struct Application
    {
        explicit Application(const FvDlssApplication& application)
            : ProjectId(application.ProjectId),
              EngineVersion(application.EngineVersion),
              DataPath(Wide(application.DataPath)),
              RuntimePath(Wide(application.RuntimePath))
        {
            Paths[0] = RuntimePath.data();
            Common.PathListInfo.Path = Paths;
            Common.PathListInfo.Length = 1;

            // NGX writes its log beside its other files in the data path.
            Common.LoggingInfo.MinimumLoggingLevel = NVSDK_NGX_LOGGING_LEVEL_ON;
            Discovery.SDKVersion = NVSDK_NGX_Version_API;
            Discovery.FeatureID = NVSDK_NGX_Feature_SuperSampling;
            Discovery.Identifier.IdentifierType = NVSDK_NGX_Application_Identifier_Type_Project_Id;
            Discovery.Identifier.v.ProjectDesc.ProjectId = ProjectId.c_str();
            Discovery.Identifier.v.ProjectDesc.EngineType = NVSDK_NGX_ENGINE_TYPE_CUSTOM;
            Discovery.Identifier.v.ProjectDesc.EngineVersion = EngineVersion.c_str();
            Discovery.ApplicationDataPath = DataPath.c_str();
            Discovery.FeatureInfo = &Common;
        }

        Application(const Application&) = delete;
        Application& operator=(const Application&) = delete;

        std::string ProjectId;
        std::string EngineVersion;
        std::wstring DataPath;
        std::wstring RuntimePath;
        wchar_t* Paths[1] = {};
        NVSDK_NGX_FeatureCommonInfo Common = {};
        NVSDK_NGX_FeatureDiscoveryInfo Discovery = {};
    };

    NVSDK_NGX_PerfQuality_Value Quality(FvDlssMode mode)
    {
        if (mode == FvDlssModeDlaa)
        {
            return NVSDK_NGX_PerfQuality_Value_DLAA;
        }

        return NVSDK_NGX_PerfQuality_Value_MaxQuality;
    }

    NVSDK_NGX_Resource_VK Resource(const FvDlssImage& image, bool readWrite)
    {
        VkImageSubresourceRange range = {image.Aspect, 0, 1, 0, 1};
        VkImageView view = reinterpret_cast<VkImageView>(image.View);
        VkImage handle = reinterpret_cast<VkImage>(image.Image);
        VkFormat format = static_cast<VkFormat>(image.Format);
        return NVSDK_NGX_Create_ImageView_Resource_VK(view, handle, range, format, image.Width, image.Height, readWrite);
    }
}

// NGX on one device, with the capability parameters every feature there is made and evaluated with.
struct FvDlssContext
{
    FvDlssContext(const FvDlssApplication& application, VkDevice device)
        : App(application),
          Device(device)
    {
    }

    Application App;
    VkDevice Device;
    NVSDK_NGX_Parameter* Parameters = nullptr;
};

uint32_t FvDlssInstanceExtensions(const FvDlssApplication* application, uint32_t* count, VkExtensionProperties** extensions)
{
    std::lock_guard<std::mutex> guard(Lock);
    Application info(*application);
    return NVSDK_NGX_VULKAN_GetFeatureInstanceExtensionRequirements(&info.Discovery, count, extensions);
}

uint32_t FvDlssDeviceExtensions(const FvDlssApplication* application, VkInstance instance, VkPhysicalDevice physicalDevice, uint32_t* count, VkExtensionProperties** extensions)
{
    std::lock_guard<std::mutex> guard(Lock);
    *count = 0;
    Application info(*application);
    NVSDK_NGX_FeatureRequirement requirement = {};
    NVSDK_NGX_Result result = NVSDK_NGX_VULKAN_GetFeatureRequirements(instance, physicalDevice, &info.Discovery, &requirement);
    if (NVSDK_NGX_FAILED(result))
    {
        return result;
    }

    if (requirement.FeatureSupported != NVSDK_NGX_FeatureSupportResult_Supported)
    {
        return NVSDK_NGX_Result_FAIL_FeatureNotSupported;
    }

    return NVSDK_NGX_VULKAN_GetFeatureDeviceExtensionRequirements(instance, physicalDevice, &info.Discovery, count, extensions);
}

uint32_t FvDlssInit(const FvDlssApplication* application, VkInstance instance, VkPhysicalDevice physicalDevice, VkDevice device, PFN_vkGetInstanceProcAddr getInstanceProcAddr, PFN_vkGetDeviceProcAddr getDeviceProcAddr, FvDlssContext** context)
{
    std::lock_guard<std::mutex> guard(Lock);
    *context = nullptr;
    std::unique_ptr<FvDlssContext> made = std::make_unique<FvDlssContext>(*application, device);
    Application& app = made->App;
    NVSDK_NGX_Result result = NVSDK_NGX_VULKAN_Init_with_ProjectID(app.ProjectId.c_str(), NVSDK_NGX_ENGINE_TYPE_CUSTOM, app.EngineVersion.c_str(), app.DataPath.c_str(), instance, physicalDevice, device, getInstanceProcAddr, getDeviceProcAddr, &app.Common, NVSDK_NGX_Version_API);
    if (NVSDK_NGX_FAILED(result))
    {
        return result;
    }

    result = NVSDK_NGX_VULKAN_GetCapabilityParameters(&made->Parameters);
    if (NVSDK_NGX_FAILED(result))
    {
        NVSDK_NGX_VULKAN_Shutdown1(device);
        return result;
    }

    int available = 0;
    NVSDK_NGX_Result query = NVSDK_NGX_Parameter_GetI(made->Parameters, NVSDK_NGX_Parameter_SuperSampling_Available, &available);
    if (NVSDK_NGX_FAILED(query) || available == 0)
    {
        // Why DLSS can't run here, such as a driver too old for it.
        int reason = NVSDK_NGX_Result_FAIL_FeatureNotSupported;
        NVSDK_NGX_Parameter_GetI(made->Parameters, NVSDK_NGX_Parameter_SuperSampling_FeatureInitResult, &reason);
        if (NVSDK_NGX_SUCCEED(reason))
        {
            reason = NVSDK_NGX_Result_FAIL_FeatureNotSupported;
        }

        NVSDK_NGX_VULKAN_DestroyParameters(made->Parameters);
        NVSDK_NGX_VULKAN_Shutdown1(device);
        return static_cast<uint32_t>(reason);
    }

    *context = made.release();
    return result;
}

uint32_t FvDlssShutdown(FvDlssContext* context)
{
    std::lock_guard<std::mutex> guard(Lock);
    std::unique_ptr<FvDlssContext> owned(context);
    NVSDK_NGX_VULKAN_DestroyParameters(owned->Parameters);
    return NVSDK_NGX_VULKAN_Shutdown1(owned->Device);
}

uint32_t FvDlssRenderSize(FvDlssContext* context, FvDlssMode mode, uint32_t outputWidth, uint32_t outputHeight, uint32_t* renderWidth, uint32_t* renderHeight)
{
    std::lock_guard<std::mutex> guard(Lock);
    unsigned int maxWidth = 0;
    unsigned int maxHeight = 0;
    unsigned int minWidth = 0;
    unsigned int minHeight = 0;
    float sharpness = 0;
    NVSDK_NGX_PerfQuality_Value quality = Quality(mode);
    return NGX_DLSS_GET_OPTIMAL_SETTINGS(context->Parameters, outputWidth, outputHeight, quality, renderWidth, renderHeight, &maxWidth, &maxHeight, &minWidth, &minHeight, &sharpness);
}

uint32_t FvDlssCreate(FvDlssContext* context, VkCommandBuffer commands, FvDlssMode mode, uint32_t renderWidth, uint32_t renderHeight, uint32_t outputWidth, uint32_t outputHeight, FvDlssFeature** feature)
{
    std::lock_guard<std::mutex> guard(Lock);
    NVSDK_NGX_DLSS_Create_Params create = {};
    create.Feature.InWidth = renderWidth;
    create.Feature.InHeight = renderHeight;
    create.Feature.InTargetWidth = outputWidth;
    create.Feature.InTargetHeight = outputHeight;
    create.Feature.InPerfQualityValue = Quality(mode);

    // The lit scene is HDR, and its motion is at the render size without the jitter.
    create.InFeatureCreateFlags = NVSDK_NGX_DLSS_Feature_Flags_IsHDR | NVSDK_NGX_DLSS_Feature_Flags_MVLowRes;
    NVSDK_NGX_Handle* handle = nullptr;
    NVSDK_NGX_Result result = NGX_VULKAN_CREATE_DLSS_EXT1(context->Device, commands, 1, 1, &handle, context->Parameters, &create);
    *feature = reinterpret_cast<FvDlssFeature*>(handle);
    return result;
}

uint32_t FvDlssEvaluate(FvDlssContext* context, VkCommandBuffer commands, FvDlssFeature* feature, const FvDlssFrame* frame)
{
    std::lock_guard<std::mutex> guard(Lock);
    NVSDK_NGX_Resource_VK color = Resource(frame->Color, false);
    NVSDK_NGX_Resource_VK depth = Resource(frame->Depth, false);
    NVSDK_NGX_Resource_VK motion = Resource(frame->Motion, false);
    NVSDK_NGX_Resource_VK exposure = Resource(frame->Exposure, false);
    NVSDK_NGX_Resource_VK output = Resource(frame->Output, true);
    NVSDK_NGX_VK_DLSS_Eval_Params evaluate = {};
    evaluate.Feature.pInColor = &color;
    evaluate.Feature.pInOutput = &output;
    evaluate.pInDepth = &depth;
    evaluate.pInMotionVectors = &motion;
    evaluate.pInExposureTexture = &exposure;
    evaluate.InJitterOffsetX = frame->JitterX;
    evaluate.InJitterOffsetY = frame->JitterY;
    evaluate.InRenderSubrectDimensions.Width = frame->RenderWidth;
    evaluate.InRenderSubrectDimensions.Height = frame->RenderHeight;
    evaluate.InReset = frame->Reset;
    evaluate.InMVScaleX = 1;
    evaluate.InMVScaleY = 1;
    NVSDK_NGX_Handle* handle = reinterpret_cast<NVSDK_NGX_Handle*>(feature);
    return NGX_VULKAN_EVALUATE_DLSS_EXT(commands, handle, context->Parameters, &evaluate);
}

uint32_t FvDlssRelease(FvDlssFeature* feature)
{
    std::lock_guard<std::mutex> guard(Lock);
    NVSDK_NGX_Handle* handle = reinterpret_cast<NVSDK_NGX_Handle*>(feature);
    return NVSDK_NGX_VULKAN_ReleaseFeature(handle);
}
