using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using BaseX;
using CloudX.Shared;
using CodeX;
using FrooxEngine;
using FrooxEngine.LogiX;
using FrooxEngine.LogiX.Data;
using FrooxEngine.LogiX.ProgramFlow;
using FrooxEngine.UIX;
using HarmonyLib;
using NeosModLoader;

namespace LagFreeContactsLoading
{
    public class LagFreeContactsLoading : NeosMod
    {
        public static ModConfiguration Config;

        [AutoRegisterConfigKey]
        internal static ModConfigurationKey<int> UpdateSpread = new ModConfigurationKey<int>("UpdateSpread", "How many updates to spread the loading of contacts over. Increase with more contacts.", () => 20);

        public override string Author => "Banane9";
        public override string Link => "https://github.com/Banane9/NeosLagFreeContactsLoading";
        public override string Name => "LagFreeContactsLoading";
        public override string Version => "1.0.0";

        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony($"{Author}.{Name}");
            Config = GetConfiguration();
            Config.Save(true);
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(FriendsDialog))]
        private static class FriendsDialogPatch
        {
            private static readonly MethodInfo addContactItemsMethod = typeof(FriendsDialogPatch).GetMethod(nameof(AddContactItems), AccessTools.allDeclared);
            private static readonly MethodInfo foreachFriendMethod = typeof(FriendManager).GetMethod("ForeachFriend", AccessTools.allDeclared);
            private static readonly MethodInfo updateFriendItemMethod = typeof(FriendsDialog).GetMethod("UpdateFriendItem", AccessTools.allDeclared);

            private static void AddContactItems(FriendsDialog friendsDialog)
            {
                var updateFriendItem = AccessTools.MethodDelegate<Action<Friend>>(updateFriendItemMethod, friendsDialog, false);

                var friends = new List<Friend>();
                friendsDialog.Engine.Cloud.Friends.GetFriends(friends);

                var friendGroups = friends.SplitToGroups(friends.Count / Config.GetValue(UpdateSpread));

                for (var i = 0; i < friendGroups.Count; ++i)
                {
                    // have to evaluate the indexing, otherwise only the last i gets closed over
                    var friendGroup = friendGroups[i];
                    friendsDialog.RunInUpdates(i, () => friendGroup.ForEach(updateFriendItem));
                }
            }

            [HarmonyTranspiler]
            [HarmonyPatch("OnAttach")]
            private static IEnumerable<CodeInstruction> OnAttachTranspiler(IEnumerable<CodeInstruction> instructionsEnumerable)
            {
                var instructions = instructionsEnumerable.ToList();

                var callIndex = instructions.FindIndex(instruction => instruction.Calls(foreachFriendMethod));

                instructions[callIndex] = new CodeInstruction(OpCodes.Call, addContactItemsMethod);
                instructions.RemoveRange(callIndex - 6, 6);

                return instructions;
            }
        }
    }
}