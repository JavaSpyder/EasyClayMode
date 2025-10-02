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
                api.Input.RegisterHotKeyFirst("set-hammer-mode-hit", "Set Hammer Mode Hit", GlKeys.O, HotkeyType.CharacterControls);
                api.Input.RegisterHotKeyFirst("set-hammer-mode-split", "Set Hammer Mode Split", GlKeys.U, HotkeyType.CharacterControls);
                api.Input.RegisterHotKeyFirst("set-hammer-mode-upsetright", "Set Hammer Mode Upset Right", GlKeys.L, HotkeyType.CharacterControls);
                api.Input.RegisterHotKeyFirst("set-hammer-mode-upsetdown", "Set Hammer Mode Upset Down", GlKeys.K, HotkeyType.CharacterControls);
                api.Input.RegisterHotKeyFirst("set-hammer-mode-upsetleft", "Set Hammer Mode Upset Left", GlKeys.J, HotkeyType.CharacterControls);
                api.Input.RegisterHotKeyFirst("set-hammer-mode-upsetup", "Set Hammer Mode Upset Up", GlKeys.I, HotkeyType.CharacterControls);
            }
            catch (Exception)
            {
                capi.Logger.Warning("Easy Hammer Mode - Failed to register hotkeys. They may already be registered.");
            }

            api.Input.SetHotKeyHandler("set-hammer-mode-upsetup", x => SetHammerMode(1));
            api.Input.SetHotKeyHandler("set-hammer-mode-upsetleft", x => SetHammerMode(4));
            api.Input.SetHotKeyHandler("set-hammer-mode-upsetdown", x => SetHammerMode(3));
            api.Input.SetHotKeyHandler("set-hammer-mode-upsetright", x => SetHammerMode(2));
            api.Input.SetHotKeyHandler("set-hammer-mode-split", x => SetHammerMode(5));
            api.Input.SetHotKeyHandler("set-hammer-mode-hit", x => SetHammerMode(0));
        }
        
        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            
            // Set up server network channel and message handler
            serverChannel = api.Network.GetChannel(ChannelName)
                .SetMessageHandler<HammerModePacket>(OnHammerModePacket);
        }
        
        private void OnHammerModePacket(IPlayer fromPlayer, HammerModePacket packet)
        {
            // Try different ways to get the hotbar inventory
            var hotbarInventory = fromPlayer.InventoryManager.GetHotbarInventory();
            var itemSlot = hotbarInventory[packet.SlotId];
            var item = itemSlot.Itemstack?.Collectible;
            if (
                hotbarInventory == null ||
                packet.SlotId < 0 || packet.SlotId >= hotbarInventory.Count ||
                itemSlot == null || itemSlot.Empty ||
                item?.GetType().Name != "ItemHammer"
            )
            {
                return;
            }

            // Set the tool mode on the server side
            item.SetToolMode(itemSlot, fromPlayer, fromPlayer.CurrentBlockSelection, packet.Mode);
        }

        public bool SetHammerMode(int mode)
        {
            // Get the client player
            var player = capi.World.Player;
            var activeSlot = player.InventoryManager.ActiveHotbarSlot;
            var item = activeSlot.Itemstack?.Collectible;
            if (
                player == null ||
                activeSlot == null || activeSlot.Empty ||
                item == null ||
                item.GetType().Name != "ItemHammer"
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
                SlotId = player.InventoryManager.ActiveHotbarSlotNumber
            };
            clientChannel.SendPacket(packet);
            
            return true;
        }