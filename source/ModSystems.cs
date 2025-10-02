using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace EasyHammerMode.source
{
    [ProtoContract]
    public class HammerModePacket
    {
        [ProtoMember(1)]
        public int Mode { get; set; }

        [ProtoMember(2)]
        public int SlotId { get; set; }
    }

    public sealed class EasyHammerModeModSystem : ModSystem
    {
        public required ICoreClientAPI capi;
        public required ICoreServerAPI sapi;
        public required IClientNetworkChannel clientChannel;
        public required IServerNetworkChannel serverChannel;

        private const string ChannelName = "easyhammermodepacket";

        public override void Start(ICoreAPI api)
        {
            // Register network channel on both sides
            api.Network
                .RegisterChannel(ChannelName)
                .RegisterMessageType<HammerModePacket>();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            // Set up client network channel
            clientChannel = api.Network.GetChannel(ChannelName);
            try
            {
                api.Input.RegisterHotKeyFirst("set-hammer-mode-upsetright", "Set Hammer Mode Upset Right", GlKeys.L, HotkeyType.CharacterControls);
                api.Input.RegisterHotKeyFirst("set-hammer-mode-upsetdown", "Set Hammer Mode Upset Down", GlKeys.K, HotkeyType.CharacterControls);
                api.Input.RegisterHotKeyFirst("set-hammer-mode-upsetleft", "Set Hammer Mode Upset Left", GlKeys.J, HotkeyType.CharacterControls);
                api.Input.RegisterHotKeyFirst("set-hammer-mode-hit", "Set Hammer Mode Hit", GlKeys.O, HotkeyType.CharacterControls);
                api.Input.RegisterHotKeyFirst("set-hammer-mode-upsetup", "Set Hammer Mode Upset Up", GlKeys.I, HotkeyType.CharacterControls);
                api.Input.RegisterHotKeyFirst("set-hammer-mode-split", "Set Hammer Mode Split", GlKeys.U, HotkeyType.CharacterControls);
            }
            catch (Exception)
            {
                capi.Logger.Warning("Easy Hammer Mode - Failed to register hotkeys. They may already be registered.");
            }

            api.Input.SetHotKeyHandler("set-hammer-mode-upsetright", x => SetHammerMode(2));
            api.Input.SetHotKeyHandler("set-hammer-mode-upsetdown", x => SetHammerMode(3));
            api.Input.SetHotKeyHandler("set-hammer-mode-upsetleft", x => SetHammerMode(4));
            api.Input.SetHotKeyHandler("set-hammer-mode-hit", x => SetHammerMode(0));
            api.Input.SetHotKeyHandler("set-hammer-mode-upsetup", x => SetHammerMode(1));
            api.Input.SetHotKeyHandler("set-hammer-mode-split", x => SetHammerMode(5));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            // Set up server network channel and message handler
            serverChannel = api.Network.GetChannel(ChannelName)
                .SetMessageHandler<HammerModePacket>(OnHammerModePacket);
        }

        public bool SetHammerMode(int mode)
        {
            var player = capi.World.Player;
            if (player == null)
            {
                return false;
            }

            IInventory hotbarInventory = null;
            ItemSlot activeSlot = null;
            int activeSlotNumber = -1;

            try
            {
                if (capi.ModLoader.IsModEnabled("xskills"))
                {
                    hotbarInventory = player.InventoryManager.GetOwnInventory("xskillshotbar");
                    if (hotbarInventory != null)
                    {
                        activeSlotNumber = player.InventoryManager.ActiveHotbarSlotNumber;
                        if (activeSlotNumber >= 0 && activeSlotNumber < hotbarInventory.Count)
                        {
                            activeSlot = hotbarInventory[activeSlotNumber];
                        }
                    }
                }

                if (hotbarInventory == null || activeSlot == null)
                {
                    hotbarInventory = player.InventoryManager.GetHotbarInventory();
                    activeSlot = player.InventoryManager.ActiveHotbarSlot;
                    activeSlotNumber = player.InventoryManager.ActiveHotbarSlotNumber;
                }
            }
            catch (Exception)
            {
                hotbarInventory = player.InventoryManager.GetHotbarInventory();
                activeSlot = player.InventoryManager.ActiveHotbarSlot;
                activeSlotNumber = player.InventoryManager.ActiveHotbarSlotNumber;
            }

            var item = activeSlot?.Itemstack?.Collectible;
            if (
                hotbarInventory == null ||
                activeSlot == null || activeSlot.Empty ||
                item == null ||
                (!item.GetType().Name.Contains("ItemHammer"))
            )
            {
                return false;
            }

            // Set the tool mode on the client side
            item.SetToolMode(activeSlot, player, player.CurrentBlockSelection, mode);

            // Send packet to server for synchronization
            var packet = new HammerModePacket
            {
                Mode = mode,
                SlotId = activeSlotNumber
            };
            clientChannel.SendPacket(packet);
            
            return true;
        }

        private void OnHammerModePacket(IPlayer fromPlayer, HammerModePacket packet)
        {
            IInventory hotbarInventory = null;
            string inventoryUsed = "unknown";

            try
            {
                if (sapi.ModLoader.IsModEnabled("xskills"))
                {
                    hotbarInventory = fromPlayer.InventoryManager.GetOwnInventory("xskillshotbar");
                    inventoryUsed = "xskillshotbar";

                    if (hotbarInventory == null || hotbarInventory.Count == 0)
                    {
                        hotbarInventory = fromPlayer.InventoryManager.GetHotbarInventory();
                        inventoryUsed = "vanilla hotbar";
                    }
                    else
                    {
                        // Check if the item is actually in the XSkills hotbar at the specified slot
                        if (packet.SlotId >= 0 && packet.SlotId < hotbarInventory.Count)
                        {
                            var testSlot = hotbarInventory[packet.SlotId];
                            if (testSlot == null || testSlot.Empty)
                            {
                                hotbarInventory = fromPlayer.InventoryManager.GetHotbarInventory();
                                inventoryUsed = "vanilla hotbar (XSkills slot empty)";
                            }
                        }
                    }
                }
                else
                {
                    hotbarInventory = fromPlayer.InventoryManager.GetHotbarInventory();
                    inventoryUsed = "vanilla hotbar";
                }
            }
            catch (Exception ex)
            {
                hotbarInventory = fromPlayer.InventoryManager.GetHotbarInventory();
                inventoryUsed = "fallback vanilla hotbar";
            }

            if (
                hotbarInventory == null ||
                packet.SlotId < 0 || packet.SlotId >= hotbarInventory.Count
            )
            {
                return;
            }

            var itemSlot = hotbarInventory[packet.SlotId];
            var item = itemSlot?.Itemstack?.Collectible;

            if (itemSlot == null || itemSlot.Empty || item == null || !item.GetType().Name.Contains("ItemHammer"))
            {
                return;
            }

            // Set the tool mode on the server side
            item.SetToolMode(itemSlot, fromPlayer, fromPlayer.CurrentBlockSelection, packet.Mode);
        }
    }
}