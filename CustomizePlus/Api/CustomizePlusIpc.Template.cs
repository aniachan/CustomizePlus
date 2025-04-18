using CustomizePlus.Api.Enums;
using ECommonsLite.EzIpcManager;
using CustomizePlus.Core.Helpers;
using System;
using CustomizePlus.Templates.Data;
using Newtonsoft.Json;
using CustomizePlus.Configuration.Data.Version2;
using CustomizePlus.Configuration.Data.Version3;
using CustomizePlus.Configuration.Helpers;
using System.Buffers.Text;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;

namespace CustomizePlus.Api;
public partial class CustomizePlusIpc
{
    /// <summary>
    /// Imports a new template from a base64 string provided over IPC.
    /// </summary>
    [EzIPC("Template.Import")]
    private int ImportProfile(string templateData, string name)
    {
        Template template = null;
        try
        {
            string json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(templateData));
            template = JsonConvert.DeserializeObject<Template>(json);
        }
        catch
        {
            return (int)ErrorCode.UnknownError;
        }

        if (template is Template tpl && tpl != null)
            _templateManager.Clone(tpl, name, true);
        else
            return (int)ErrorCode.UnknownError;
        return (int)ErrorCode.Success;
    }

    private Template? GetTemplateFromV2Profile(string json)
    {
        var profile = JsonConvert.DeserializeObject<Version2Profile>(json);
        if (profile != null)
        {
            var v3Profile = V2ProfileToV3Converter.Convert(profile);

            (var _, var template) = V3ProfileToV4Converter.Convert(v3Profile);

            if (template != null)
                return template;
        }

        return null;
    }

    private Template? GetTemplateFromV3Profile(string json)
    {
        var profile = JsonConvert.DeserializeObject<Version3Profile>(json);
        if (profile != null)
        {
            if (profile.ConfigVersion != 3)
                throw new Exception("Incompatible profile version");

            (var _, var template) = V3ProfileToV4Converter.Convert(profile);

            if (template != null)
                return template;
        }

        return null;
    }
}

