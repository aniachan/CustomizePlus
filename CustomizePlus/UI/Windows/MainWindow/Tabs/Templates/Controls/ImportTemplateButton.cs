using CustomizePlus.Api.Data;
using CustomizePlus.Configuration.Data.Version2;
using CustomizePlus.Configuration.Data.Version3;
using CustomizePlus.Configuration.Helpers;
using CustomizePlus.Core.Data;
using CustomizePlus.Core.Helpers;
using CustomizePlus.Templates;
using CustomizePlus.Templates.Data;
using Newtonsoft.Json;

namespace CustomizePlus.UI.Windows.MainWindow.Tabs.Templates.Controls;

public sealed class ImportTemplateButton(
    TemplateManager templateManager,
    TemplateEditorManager editorManager,
    PopupSystem popupSystem) : BaseIconButton<AwesomeIcon>
{
    private string _clipboardText = string.Empty;

    public override AwesomeIcon Icon
        => LunaStyle.ImportIcon;

    public override bool HasTooltip
        => true;

    public override void DrawTooltip()
        => Im.Text("Try to import a design from your clipboard."u8);

    public override void OnClick()
    {
        if (editorManager.IsEditorActive)
        {
            popupSystem.ShowPopup(PopupSystem.Messages.TemplateEditorActiveWarning);
            return;
        }

        try
        {
            _clipboardText = Im.Clipboard.GetUtf16();
            Im.Popup.Open("##ImportTemplate"u8);
        }
        catch (Exception)
        {
            popupSystem.ShowPopup(PopupSystem.Messages.ActionError);
        }
    }

    protected override void PostDraw()
    {
        if (!InputPopup.OpenName("##ImportTemplate"u8, out var newName))
            return;

        if (_clipboardText.Length is 0)
            return;

        try
        {
            var importVer = Base64Helper.ImportFromBase64(_clipboardText, out var json);

            var template = Convert.ToInt32(importVer) switch
            {
                2 => GetTemplateFromV2Profile(json),
                3 => GetTemplateFromV3Profile(json),
                4 or 5 or 6 or 7 => JsonConvert.DeserializeObject<Template>(json),
                _ => null
            };

            // Clipboard data may also be a plain (non-compressed) base64 JSON payload,
            // e.g. the IPCCharacterProfile JSON returned by Profile.GetByUniqueId and
            // copied to clipboard by other plugins such as xivclone.
            template ??= GetTemplateFromPlainBase64(_clipboardText);

            if (template is Template tpl && tpl != null)

            {
                var createdTpl = templateManager.Clone(tpl, newName, true);
                templateManager.SetSource(createdTpl, DataSource.ClipboardImport);
            }
            else
                popupSystem.ShowPopup(PopupSystem.Messages.ClipboardDataUnsupported);

        }
        catch (Exception ex)
        {
            Logger.GlobalPluginLogger.Error($"Error while performing clipboard/clone/create template action: {ex}");
            popupSystem.ShowPopup(PopupSystem.Messages.ActionError);
        }
        finally
        {
            _clipboardText = string.Empty;
        }
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

    /// <summary>
    /// Fallback for clipboard payloads that are plain (non-gzip) base64 JSON,
    /// matching the format used by the Template.Import and Profile.GetByUniqueId
    /// IPC endpoints. Tries a direct template deserialization first, then an
    /// IPCCharacterProfile-shaped payload.
    /// </summary>
    private Template? GetTemplateFromPlainBase64(string base64)
    {
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64.Trim()));

            var template = JsonConvert.DeserializeObject<Template>(json);
            if (template != null && template.Bones.Count > 0)
                return template;

            var ipcProfile = JsonConvert.DeserializeObject<IPCCharacterProfile>(json);
            if (ipcProfile != null && ipcProfile.Bones.Count > 0)
                return new Template(ipcProfile);
        }
        catch
        {
            // Not plain base64 JSON either; caller will show the unsupported popup.
        }

        return null;
    }
}
