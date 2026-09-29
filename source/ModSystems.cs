using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace EasyClayMode.source
{
    [ProtoContract]
    public class ClayModePacket
    {
        [ProtoMember(1)]
        public int Mode { get; set; }

        [ProtoMember(2)]
        public int SlotId { get; set; }
    }

    public sealed class EasyClayModeModSystem : ModSystem
    {
        public required ICoreClientAPI capi;
        public required ICoreServerAPI sapi;
        public required IClientNetworkChannel clientChannel;
        public required IServerNetworkChannel serverChannel;

        private const string ChannelName = "EasyClayModepacket";

        public override void Start(ICoreAPI api)
        {
            // Register network channel on both sides
            api.Network
                .RegisterChannel(ChannelName)
                .RegisterMessageType<ClayModePacket>();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            // Set up client network channel
            clientChannel = api.Network.GetChannel(ChannelName);

            api.Input.RegisterHotKeyFirst("set-clay-mode-1size", "Set Clay Mode 1x1", GlKeys.J, HotkeyType.CharacterControls);
            api.Input.RegisterHotKeyFirst("set-clay-mode-2size", "Set Clay Mode 2x2", GlKeys.K, HotkeyType.CharacterControls);
            api.Input.RegisterHotKeyFirst("set-clay-mode-3size", "Set Clay Mode 3x3", GlKeys.L, HotkeyType.CharacterControls);
            api.Input.RegisterHotKeyFirst("set-clay-mode-duplicate", "Set Clay Mode Duplicate", GlKeys.I, HotkeyType.CharacterControls);

            api.Input.SetHotKeyHandler("set-clay-mode-1size", x => SetClayMode(0));
            api.Input.SetHotKeyHandler("set-clay-mode-2size", x => SetClayMode(1));
            api.Input.SetHotKeyHandler("set-clay-mode-3size", x => SetClayMode(2));
            api.Input.SetHotKeyHandler("set-clay-mode-duplicate", x => SetClayMode(3));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            // Set up server network channel and message handler
            serverChannel = api.Network.GetChannel(ChannelName)
                .SetMessageHandler<ClayModePacket>(OnClayModePacket);
        }

        public bool SetClayMode(int mode)
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
                (!item.GetType().Name.Contains("ItemClay"))
            )
            {
                return false;
            }

            // Set the tool mode on the client side
            item.SetToolMode(activeSlot, player, player.CurrentBlockSelection, mode);

            // Send packet to server for synchronization
            var packet = new ClayModePacket
            {
                Mode = mode,
                SlotId = activeSlotNumber
            };
            clientChannel.SendPacket(packet);
            
            return true;
        }

        private void OnClayModePacket(IPlayer fromPlayer, ClayModePacket packet)
        {
            IInventory hotbarInventory = null;

            try
            {
                if (sapi.ModLoader.IsModEnabled("xskills"))
                {
                    hotbarInventory = fromPlayer.InventoryManager.GetOwnInventory("xskillshotbar");

                    if (hotbarInventory == null || hotbarInventory.Count == 0)
                    {
                        hotbarInventory = fromPlayer.InventoryManager.GetHotbarInventory();
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
                            }
                        }
                    }
                }
                else
                {
                    hotbarInventory = fromPlayer.InventoryManager.GetHotbarInventory();
                }
            }
            catch (Exception)
            {
                hotbarInventory = fromPlayer.InventoryManager.GetHotbarInventory();
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

            if (itemSlot == null || itemSlot.Empty || item == null || !item.GetType().Name.Contains("ItemClay"))
            {
                return;
            }

            // Set the tool mode on the server side
            item.SetToolMode(itemSlot, fromPlayer, fromPlayer.CurrentBlockSelection, packet.Mode);
        }
    }
}