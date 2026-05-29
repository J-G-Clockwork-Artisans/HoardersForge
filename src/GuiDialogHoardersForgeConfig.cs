using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace HoardersForge
{
    public class GuiDialogHoardersForgeConfig : GuiDialog
    {
        private readonly HoardersForgeConfig _config;

        public override string ToggleKeyCombinationCode => null; // Opened via command or registered hotkey

        public GuiDialogHoardersForgeConfig(ICoreClientAPI capi, HoardersForgeConfig config) : base(capi)
        {
            _config = config;
        }

        public override void OnGuiOpened()
        {
            SetupDialog();
        }

        private void SetupDialog()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fixed(0, 0, 400, 250);

            var composer = capi.Gui.CreateCompo("hoardersforgeconfig", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("Hoarder's Forge Config", () => TryClose())
                .BeginChildElements(bgBounds);

            // 1. Debug Logging Switch
            composer.AddStaticText("Enable Debug Logging:", CairoFont.WhiteSmallText(), ElementBounds.Fixed(20, 60, 200, 30).WithParent(bgBounds))
                    .AddSwitch((on) => { _config.DebugLogging = on; }, ElementBounds.Fixed(230, 60, 60, 30).WithParent(bgBounds), "debugSwitch");

            // 2. Loss Percentage Slider
            composer.AddStaticText("Recycling Loss:", CairoFont.WhiteSmallText(), ElementBounds.Fixed(20, 110, 200, 30).WithParent(bgBounds))
                    .AddSlider((val) => { _config.LossPercentage = val; return true; }, ElementBounds.Fixed(230, 105, 130, 30).WithParent(bgBounds), "lossSlider");

            // 3. Save & Close Button
            composer.AddButton("Save & Close", OnSaveAndClose, ElementBounds.Fixed(130, 180, 140, 35).WithParent(bgBounds));

            composer.EndChildElements();

            // Set initial state values
            composer.GetSwitch("debugSwitch").SetValue(_config.DebugLogging);
            composer.GetSlider("lossSlider").SetValues((int)_config.LossPercentage, 0, 100, 1, "%");

            SingleComposer = composer.Compose();
        }

        private bool OnSaveAndClose()
        {
            // Send config update packet to server
            var packet = new ConfigSyncPacket
            {
                DebugLogging = _config.DebugLogging,
                LossPercentage = _config.LossPercentage
            };
            capi.Network.GetChannel("hoardersforgeconfig").SendPacket(packet);

            TryClose();
            return true;
        }
    }
}
