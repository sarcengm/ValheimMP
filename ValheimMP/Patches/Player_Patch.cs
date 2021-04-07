using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using ValheimMP.Framework.Extensions;
using static Player;

namespace ValheimMP.Patches
{

    [HarmonyPatch]
    internal class Player_Patch
    {
        /// <summary>
        /// Turn on to suppress any message generated.
        /// </summary>
        internal static bool SuppressMessages { get; set; }

        private static GameObject m_debugZdoPlayerLocationObject;
        private static float m_lastSyncTime;
        private static float m_playerTickRate = 1.0f / 60f;
        private static bool m_isBlocking;
        private static HashSet<string> m_hairs;
        private static HashSet<int> m_models;
        private static HashSet<string> m_beards;

        [HarmonyPatch(typeof(Player), "Message")]
        [HarmonyPrefix]
        private static bool Message(MessageHud.MessageType type, string msg, int amount, Sprite icon)
        {
            return !SuppressMessages;
        }

        [HarmonyPatch(typeof(Player), "Awake")]
        [HarmonyPostfix]
        private static void Awake(Player __instance)
        {
            if (m_beards == null)
            {
                m_beards = new HashSet<string>(ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Customization, "Beard").Select(k => k.name));
                m_hairs = new HashSet<string>(ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Customization, "Hair").Select(k => k.name));
                m_models = new HashSet<int> { 0, 1 };
                m_beards.Add("BeardNone");
                m_hairs.Add("HairNone");
            }

            __instance.m_delayedDamage = new Queue<KeyValuePair<float, HitData>>();
            var zdo = __instance.m_nview.m_zdo;

            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                __instance.m_nview.Register("PlayerReady", (long sender) =>
                {
                    RPC_PlayerReady(__instance, sender);
                });
                __instance.m_nview.Register("ConsumeItem", (long sender, int itemId, ZDOID inventoryId) =>
                {
                    RPC_ConsumeItem(__instance, sender, itemId, inventoryId);
                });
                __instance.m_nview.Register("SetCraftingStation", (long sender, ZDOID stationId) =>
                {
                    RPC_SetCraftingStation(__instance, sender, stationId);
                });
                __instance.m_nview.Register("PlacePiece", (long sender, int pieceHash, Vector3 position, Quaternion rotation) =>
                {
                    RPC_PlacePiece(__instance, sender, pieceHash, position, rotation);
                });
                __instance.m_nview.Register("RemovePiece", (long sender, ZDOID id) =>
                {
                    RPC_RemovePiece(__instance, sender, id);
                });
                __instance.m_nview.Register("RepairPiece", (long sender, ZDOID id) =>
                {
                    RPC_RepairPiece(__instance, sender, id);
                });
                __instance.m_nview.Register("AddKnownText", (long sender, string label, string text) =>
                {
                    RPC_AddKnownText(__instance, sender, label, text);
                });
                __instance.m_nview.Register("SetSeenTutorial", (long sender, string name) =>
                {
                    RPC_SetSeenTutorial(__instance, sender, name);
                });
                __instance.m_nview.Register("StartEmote", (long sender, string name, bool oneshot) =>
                {
                    RPC_StartEmote(__instance, sender, name, oneshot);
                });
                __instance.m_nview.Register("StopEmote", (long sender) =>
                {
                    RPC_StopEmote(__instance, sender);
                });
                __instance.m_nview.Register("HideHandItems", (long sender) =>
                {
                    RPC_HideHandItems(__instance, sender);
                });
                __instance.m_nview.Register("Jump", (long sender) =>
                {
                    RPC_Jump(__instance, sender);
                });
                __instance.m_nview.Register("StartAttack", (long sender) =>
                {
                    RPC_StartAttack(__instance, sender);
                });
                __instance.m_nview.Register("StartSecondaryAttack", (long sender) =>
                {
                    RPC_StartSecondaryAttack(__instance, sender);
                });
                __instance.m_nview.Register("StartDrawAttack", (long sender) =>
                {
                    RPC_StartDrawAttack(__instance, sender);
                });
                __instance.m_nview.Register("ReleaseDrawAttack", (long sender) =>
                {
                    RPC_ReleaseDrawAttack(__instance, sender);
                });
                __instance.m_nview.Register("CancelDrawAttack", (long sender) =>
                {
                    RPC_CancelDrawAttack(__instance, sender);
                });
                __instance.m_nview.Register("StartBlocking", (long sender) =>
                {
                    RPC_StartBlocking(__instance, sender);
                });
                __instance.m_nview.Register("StopBlocking", (long sender) =>
                {
                    RPC_StopBlocking(__instance, sender);
                });
                __instance.m_nview.Register("Dodge", (long sender, Vector3 dodgeDir) =>
                {
                    RPC_Dodge(__instance, sender, dodgeDir);
                });
                __instance.m_nview.Register("Crouch", (long sender, bool crouch) =>
                {
                    RPC_Crouch(__instance, sender, crouch);
                });

                __instance.m_nview.Register("ClientMeleeHit", (long sender, ZPackage pkg) =>
                {
                    Attack_Patch.RPC_ClientMeleeHit(__instance, __instance.m_currentAttack ?? __instance.m_previousAttack, pkg);
                });
                __instance.m_nview.Register("SetAppearance", (long sender, ZPackage pkg) =>
                {
                    RPC_SetAppearance(__instance, sender, pkg);
                });
            }
            else if (__instance.IsOwner())
            {
                // Removed sender from client side RPCs it's always the server.
                // So removed, lest it gets confusing and I try to confirm if the sender is the client.
                __instance.m_nview.Register("ClientConsume", (long sender, int itemHash) =>
                {
                    RPC_ClientConsume(__instance, itemHash);
                });
                __instance.m_nview.Register("SyncCharacter", (long sender, ZPackage pkg) =>
                {
                    RPC_SyncCharacter(__instance, pkg);
                });
                __instance.m_nview.Register("TeleportTo", (long sender, Vector3 pos, Quaternion rot, bool distantTeleport) =>
                {
                    RPC_TeleportTo(__instance, pos, rot, distantTeleport);
                });
                __instance.m_nview.Register("Pushback", (long sender, Vector3 pushForce) =>
                {
                    RPC_Pushback(__instance, pushForce);
                });
                zdo.SetFieldType("stamina", ZDOFieldType.Private);
                zdo.RegisterZDOEvent("stamina", (ZDO zdo) =>
                {
                    ZDOEvent_SyncStamina(__instance);
                });
                zdo.RegisterZDOEvent("health", (ZDO zdo) =>
                {
                    ZDOEvent_SyncHealth(__instance);
                });
                zdo.RegisterZDOEvent("pvp", (ZDO zdo) =>
                {
                    ZDOEvent_PVPChanged(__instance);
                });
                zdo.RegisterZDOEvent("forcedpvp", (ZDO zdo) =>
                {
                    ZDOEvent_PVPChanged(__instance);
                });
                zdo.RegisterZDOEvent("noPlacementCost", (ZDO zdo) =>
                {
                    ZDOEvent_NoPlacementCostChanged(__instance);
                });
                zdo.RegisterZDOEvent("ModelIndex", (ZDO zdo) =>
                {
                    ZDOEvent_PlayerModelChanged(__instance);
                });
                zdo.RegisterZDOEvent("SkinColor", (ZDO zdo) =>
                {
                    ZDOEvent_SkinColorChanged(__instance);
                });
                zdo.RegisterZDOEvent("BeardItem", (ZDO zdo) =>
                {
                    ZDOEvent_BeardItemChanged(__instance);
                });
                zdo.RegisterZDOEvent("HairItem", (ZDO zdo) =>
                {
                    ZDOEvent_HairItemChanged(__instance);
                });
                zdo.RegisterZDOEvent("HairColor", (ZDO zdo) =>
                {
                    ZDOEvent_HairColorChanged(__instance);
                });

                __instance.SetLocalPlayer();
                __instance.m_isLoading = true;
                CheckPlayerReady();

                // Update all appearance fields so the client side profile gets it right.
                ZDOEvent_BeardItemChanged(__instance);
                ZDOEvent_HairItemChanged(__instance);
                ZDOEvent_PlayerModelChanged(__instance);
                ZDOEvent_SkinColorChanged(__instance);
                ZDOEvent_HairColorChanged(__instance);
            }

            if (zdo != null)
            {
                zdo.RegisterZDOEvent("m_attachPoint", (ZDO zdo) =>
                {
                    ZDOEvent_Attach(__instance);
                });
            }
        }

        private static string GetHair(int hash)
        {
            foreach (var hair in m_hairs)
            {
                if (hair.GetStableHashCode() == hash)
                {
                    return hair;
                }
            }
            return "HairNone";
        }

        private static string GetBeard(int hash)
        {
            foreach (var beard in m_beards)
            {
                if (beard.GetStableHashCode() == hash)
                {
                    return beard;
                }
            }
            return "BeardNone";
        }

        private static void ZDOEvent_HairColorChanged(Player player)
        {
            player.m_hairColor = player.m_nview.m_zdo.GetVec3("HairColor", player.m_hairColor);
        }

        private static void ZDOEvent_HairItemChanged(Player player)
        {
            player.m_hairItem = GetHair(player.m_nview.m_zdo.GetInt("HairItem", player.m_hairItem.GetStableHashCode()));
        }


        private static void ZDOEvent_BeardItemChanged(Player player)
        {
            player.m_beardItem = GetBeard(player.m_nview.m_zdo.GetInt("BeardItem", player.m_beardItem.GetStableHashCode()));
        }

        private static void ZDOEvent_SkinColorChanged(Player player)
        {
            player.m_skinColor = player.m_nview.m_zdo.GetVec3("SkinColor", player.m_skinColor);
        }

        private static void ZDOEvent_PlayerModelChanged(Player player)
        {
            player.m_modelIndex = player.m_nview.m_zdo.GetInt("ModelIndex", player.m_modelIndex);
        }

        private static void ZDOEvent_NoPlacementCostChanged(Player player)
        {
            player.m_noPlacementCost = player.m_nview.m_zdo.GetBool("noPlacementCost");
        }

        private static void ZDOEvent_PVPChanged(Player player)
        {
            player.SetPVP(player.m_nview.m_zdo.GetBool("pvp") || player.m_nview.m_zdo.GetBool("forcedpvp"));
        }

        private static void RPC_SetAppearance(Player player, long sender, ZPackage pkg)
        {
            if (!player.m_firstSpawn)
                return;
            player.m_firstSpawn = false;

            var beard = pkg.ReadString();
            var hair = pkg.ReadString();
            var hairColor = pkg.ReadVector3();
            var skinColor = pkg.ReadVector3();
            var modelIndex = pkg.ReadInt();

            if (beard != "" && !m_beards.Contains(beard))
            {
                ValheimMP.Log($"Player {player.GetPlayerName()} ({player.GetPlayerID()}) selected invalid beard: {beard} ");
                return;
            }

            if (hair != "" && !m_hairs.Contains(hair))
            {
                ValheimMP.Log($"Player {player.GetPlayerName()} ({player.GetPlayerID()}) selected invalid hair: {hair} ");
                return;
            }

            if (modelIndex != -1 && !m_models.Contains(modelIndex))
            {
                ValheimMP.Log($"Player {player.GetPlayerName()} ({player.GetPlayerID()}) selected invalid hair: {hair} ");
                return;
            }

            if (beard != "")
                player.SetBeard(beard);
            if (hair != "")
                player.SetHair(hair);
            if (hairColor != Vector3.zero)
                player.SetHairColor(hairColor);
            if (skinColor != Vector3.zero)
                player.SetSkinColor(skinColor);
            if (modelIndex != -1)
                player.SetPlayerModel(modelIndex);
        }

        private static void SetAppearance(Player player)
        {
            var pkg = new ZPackage();
            pkg.Write(FejdStartup_Patch.m_beardItem);
            pkg.Write(FejdStartup_Patch.m_hairItem);
            pkg.Write(FejdStartup_Patch.m_hairColor);
            pkg.Write(FejdStartup_Patch.m_skinColor);
            pkg.Write(FejdStartup_Patch.m_modelIndex);

            player.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "SetAppearance", pkg);
        }

        private static void ZDOEvent_SyncStamina(Player __instance)
        {
            if (__instance is null)
                return;
            var targetStamina = __instance.m_nview.m_zdo.GetFloat("stamina");

            if (Mathf.Abs(__instance.m_stamina - targetStamina) > 5f)
            {
                __instance.m_stamina = targetStamina;
            }
        }

        private static void ZDOEvent_SyncHealth(Player __instance)
        {
            if (__instance is null)
                return;
            var targetHealth = __instance.m_nview.m_zdo.GetFloat("health");

            if (Mathf.Abs(__instance.m_health - targetHealth) > 1f)
            {
                __instance.m_health = targetHealth;
            }
        }

        [HarmonyPatch(typeof(Player), "OnDestroy")]
        [HarmonyPrefix]
        private static void OnDestroy(Player __instance)
        {
            if (__instance.m_nview != null && __instance.m_nview.m_zdo != null)
            {
                __instance.m_nview.m_zdo.ClearZDOEvent("m_attachPoint");
            }
        }


        [HarmonyPatch(typeof(Character), "ApplyPushback")]
        [HarmonyPostfix]
        private static void ApplyPushback(Character __instance)
        {
            if (ValheimMP.IsDedicated && __instance is Player player)
            {
                player.m_nview.InvokeRPC("Pushback", __instance.m_pushForce);
            }
        }

        private static void RPC_Pushback(Player __instance, Vector3 pushForce)
        {
            __instance.m_pushForce = pushForce;
        }

        public static void CheckPlayerReady()
        {
            var player = Player.m_localPlayer;

            SetAppearance(player);

            player.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "PlayerReady");
            player.m_firstSpawn = false;

            var game = Game.instance;
            if (game.m_firstSpawn)
            {
                game.m_firstSpawn = false;
                Chat.instance.SendText(Talker.Type.Shout, "Have I arrived!?!");
            }
        }

        private static ZPackage SerializeFoods(Player __instance, ZPackage pkg)
        {
            pkg.Write(__instance.m_foods.Count);
            foreach (var food in __instance.m_foods)
            {
                pkg.Write(food.m_name);
                pkg.Write(food.m_health);
                pkg.Write(food.m_stamina);
            }
            return pkg;
        }

        private static void DeserializeFoods(Player __instance, ZPackage pkg)
        {
            __instance.m_foods.Clear();
            var count = pkg.ReadInt();
            for (int i = 0; i < count; i++)
            {
                var food = new Food
                {
                    m_name = pkg.ReadString(),
                    m_health = pkg.ReadSingle(),
                    m_stamina = pkg.ReadSingle()
                };
                var itemPrefab = ObjectDB.instance.GetItemPrefab(food.m_name);
                if (itemPrefab == null)
                {
                    ValheimMP.LogWarning("Failed to find food item " + food.m_name);
                    continue;
                }
                food.m_item = itemPrefab.GetComponent<ItemDrop>().m_itemData;
                __instance.m_foods.Add(food);
            }
            __instance.UpdateFood(0f, forceUpdate: true);
        }

        private static ZPackage SerializeKnowledge(Player __instance, ZPackage pkg)
        {
            pkg.Write(__instance.m_knownRecipes.Count);
            foreach (var item in __instance.m_knownRecipes)
            {
                pkg.Write(item);
            }

            pkg.Write(__instance.m_knownStations.Count);
            foreach (var item in __instance.m_knownStations)
            {
                pkg.Write(item.Key);
                pkg.Write(item.Value);
            }

            pkg.Write(__instance.m_shownTutorials.Count);
            foreach (var item in __instance.m_shownTutorials)
            {
                pkg.Write(item);
            }

            pkg.Write(__instance.m_uniques.Count);
            foreach (var item in __instance.m_uniques)
            {
                pkg.Write(item);
            }

            pkg.Write(__instance.m_trophies.Count);
            foreach (var item in __instance.m_trophies)
            {
                pkg.Write(item);
            }

            pkg.Write(__instance.m_knownMaterial.Count);
            foreach (var item in __instance.m_knownMaterial)
            {
                pkg.Write(item);
            }

            pkg.Write(__instance.m_knownBiome.Count);
            foreach (var item in __instance.m_knownBiome)
            {
                pkg.Write((int)item);
            }

            pkg.Write(__instance.m_knownTexts.Count);
            foreach (var item in __instance.m_knownTexts)
            {
                pkg.Write(item.Key);
                pkg.Write(item.Value);
            }

            return pkg;
        }

        private static void DeserializeKnowledge(Player __instance, ZPackage pkg)
        {
            var m_knownRecipes = pkg.ReadInt();
            for (int i = 0; i < m_knownRecipes; i++)
            {
                __instance.m_knownRecipes.Add(pkg.ReadString());
            }

            var m_knownStations = pkg.ReadInt();
            for (int i = 0; i < m_knownStations; i++)
            {
                var key = pkg.ReadString();
                var value = pkg.ReadInt();
                __instance.m_knownStations.AddUnique(key, value);
            }

            var m_shownTutorials = pkg.ReadInt();
            for (int i = 0; i < m_shownTutorials; i++)
            {
                __instance.m_shownTutorials.Add(pkg.ReadString());
            }

            var m_uniques = pkg.ReadInt();
            for (int i = 0; i < m_uniques; i++)
            {
                __instance.m_uniques.Add(pkg.ReadString());
            }

            var m_trophies = pkg.ReadInt();
            for (int i = 0; i < m_trophies; i++)
            {
                __instance.m_trophies.Add(pkg.ReadString());
            }

            var m_knownMaterial = pkg.ReadInt();
            for (int i = 0; i < m_knownMaterial; i++)
            {
                __instance.m_knownMaterial.Add(pkg.ReadString());
            }

            var m_knownBiome = pkg.ReadInt();
            for (int i = 0; i < m_knownBiome; i++)
            {
                __instance.m_knownBiome.Add((Heightmap.Biome)pkg.ReadInt());
            }

            var m_knownTexts = pkg.ReadInt();
            for (int i = 0; i < m_knownTexts; i++)
            {
                var key = pkg.ReadString();
                var value = pkg.ReadString();
                __instance.m_knownTexts.AddUnique(key, value);
            }
        }


        private static void RPC_SyncCharacter(Player __instance, ZPackage pkg)
        {
            pkg = pkg.Decompress();

            var skills = __instance.GetComponent<Skills>();

            skills.Load(pkg);
            DeserializeKnowledge(__instance, pkg);
            DeserializeFoods(__instance, pkg);

            __instance.m_isLoading = false;
        }

        /// <summary>
        /// Sync all things that need to be synced once on load.
        /// </summary>
        /// <param name="__instance"></param>
        private static void SyncCharacter(Player __instance)
        {
            var pkg = new ZPackage();
            var skills = __instance.GetComponent<Skills>();

            skills.Save(pkg);
            SerializeKnowledge(__instance, pkg);
            SerializeFoods(__instance, pkg);

            __instance.m_nview.InvokeRPC("SyncCharacter", pkg.Compress());
        }

        private static bool RPC_PlayerReady(Player __instance, long sender)
        {
            if (__instance.GetOwner() == sender && ZNet.instance.IsServer())
            {
                // Add an inventory listener for their own inventory!
                ValheimMP.Instance.InventoryManager.AddListener(sender, __instance.m_inventory);

                SyncCharacter(__instance);
            }

            return false;
        }


        [HarmonyPatch(typeof(Player), "AlwaysRotateCamera")]
        [HarmonyPrefix]
        private static bool AlwaysRotateCamera(Player __instance, ref bool __result)
        {
            if (!ZNet.instance.IsServer())
                return true;


            if ((__instance.GetCurrentWeapon() != null &&
                 __instance.m_currentAttack != null &&
                 __instance.m_lastCombatTimer < 1f &&
                 __instance.m_currentAttack.m_attackType != Attack.AttackType.None) ||
                __instance.IsHoldingAttack() ||
                __instance.m_blocking ||
                __instance.m_lastMoveFlags == 1)
            {
                __result = true;
                return false;
            }

            __result = false;
            return false;
        }

        [HarmonyPatch(typeof(Player), "UpdateEyeRotation")]
        [HarmonyPrefix]
        private static bool UpdateEyeRotation(Player __instance)
        {
            if (!ValheimMP.IsDedicated)
                return true;
            __instance.m_eye.rotation = Quaternion.LookRotation(__instance.m_lookDir);
            __instance.m_lookPitch = __instance.m_eye.rotation.eulerAngles.x;
            __instance.m_lookYaw = Quaternion.Euler(0, __instance.m_eye.rotation.eulerAngles.y, 0);
            return false;
        }

        private static bool ShowTutorial(Player __instance)
        {
            if (ValheimMP.IsDedicated)
                return false;

            return !__instance.m_isLoading;
        }

        [HarmonyPatch(typeof(Player), "RPC_UseStamina")]
        [HarmonyPrefix]
        private static bool RPC_UseStamina(ref Player __instance, long sender, float v)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }

        [HarmonyPriority(Priority.High)]
        [HarmonyPatch(typeof(Player), "PlacePiece")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PlacePiece(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                //TODO: Use a proper way to find this: UnityEngine.GameObject Instantiate[GameObject](UnityEngine.GameObject, UnityEngine.Vector3, UnityEngine.Quaternion)
                if (list[i].operand != null &&
                    list[i].operand.ToString() == "UnityEngine.GameObject Instantiate[GameObject](UnityEngine.GameObject, UnityEngine.Vector3, UnityEngine.Quaternion)")
                {
                    list[i - 3].opcode = OpCodes.Ldarg_1; // we don't want gameObject, we want piece.
                    list[i] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Player_Patch), "PlacePiece_Instantiate"));
                    list.Insert(i - 3, new CodeInstruction(OpCodes.Ldarg_0)); // we want player (this)
                }
            }
            return list;
        }

        private static GameObject PlacePiece_Instantiate(Player __instance, Piece piece, Vector3 position, Quaternion rotation)
        {
            if (ZNet.instance.IsServer())
            {
                // Only on the server do we actually instantiat it.
                return UnityEngine.Object.Instantiate(piece.gameObject, position, rotation);
            }

            __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "PlacePiece", piece.name.GetStableHashCode(), position, rotation);

            // Create a dummy game object that is destroyed right after
            var dummyGameObject = new GameObject();
            UnityEngine.GameObject.Destroy(dummyGameObject, 0.001f);
            return dummyGameObject;
        }


        private static GameObject GetPieceFromHash(Player __instance, int pieceHash)
        {

            foreach (var a in __instance.m_buildPieces.m_pieces)
            {
                var hash = a.name.GetStableHashCode();
                if (hash == pieceHash)
                    return a;
            }

            return null;
        }

        private static void RPC_PlacePiece(Player __instance, long sender, int pieceHash, Vector3 position, Quaternion rotation)
        {
            if (__instance.GetPlayerID() != sender)
                return;

            var obj = GetPieceFromHash(__instance, pieceHash);
            if (obj == null)
            {
                ValheimMP.Log($"RPC_PlacePiece from {sender} missing obj {pieceHash}");
                return;
            }

            Piece piece = obj.GetComponent<Piece>();
            if (piece == null)
                return;

            if (Location.IsInsideNoBuildLocation(position))
            {
                return;
            }


            var radius = 0f;
            var privateArea = piece.GetComponent<PrivateArea>();
            if (privateArea != null)
                radius += privateArea.m_radius;

            var collider = piece.GetComponent<Collider>();
            if (collider != null)
                radius += collider.bounds.extents.magnitude;

            if (!PrivateArea_Patch.CheckAccess(sender, position, radius))
            {
                return;
            }

            ItemDrop.ItemData rightItem = __instance.GetRightItem();
            if (!__instance.HaveStamina(rightItem.m_shared.m_attack.m_attackStamina))
            {
                return;
            }
            if (!__instance.HaveRequirements(piece, RequirementMode.CanBuild))
            {
                return;
            }


            PlacePieceExtract(__instance, position, rotation, obj, piece);

            __instance.ConsumeResources(piece.m_resources, 0);
            __instance.UseStamina(rightItem.m_shared.m_attack.m_attackStamina);
            if (rightItem.m_shared.m_useDurability)
            {
                rightItem.m_durability -= rightItem.m_shared.m_useDurabilityDrain;
            }
        }

        [HarmonyPatch(typeof(Player), "ConsumeResources")]
        [HarmonyPrefix]
        private static bool ConsumeResources()
        {
            // Suppress this action on clients,
            // resources are not consumed on clients,
            // lest it leads to inventory desync.
            return ZNet.instance.IsServer();
        }

        // PlacePiece, copy pasted from the game, once it succeeds it does all this
        private static void PlacePieceExtract(Player __instance, Vector3 position, Quaternion rotation, GameObject gameObject, Piece piece)
        {
            TerrainModifier.SetTriggerOnPlaced(trigger: true);
            GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject, position, rotation);
            TerrainModifier.SetTriggerOnPlaced(trigger: false);
            CraftingStation componentInChildren = gameObject2.GetComponentInChildren<CraftingStation>();
            if ((bool)componentInChildren)
            {
                __instance.AddKnownStation(componentInChildren);
            }
            Piece component = gameObject2.GetComponent<Piece>();
            if ((bool)component)
            {
                component.SetCreator(__instance.GetPlayerID());
            }
            PrivateArea component2 = gameObject2.GetComponent<PrivateArea>();
            if ((bool)component2)
            {
                component2.Setup(__instance.GetPlayerName());
            }
            WearNTear component3 = gameObject2.GetComponent<WearNTear>();
            if ((bool)component3)
            {
                component3.OnPlaced();
            }
            ItemDrop.ItemData rightItem = __instance.GetRightItem();
            if (rightItem != null)
            {
                __instance.FaceLookDirection();
                __instance.m_zanim.SetTrigger(rightItem.m_shared.m_attack.m_attackAnimation);
            }
            __instance.AddNoise(50f);
        }

        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        [HarmonyPrefix]
        private static bool UpdatePlacementGhost()
        {
            return !ZNet.instance.IsServer();
        }

        [HarmonyPatch(typeof(Player), "FixedUpdate")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> T_FixedUpdate(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            list.RemoveRange(4, list.Count - 4);
            list.Add(new CodeInstruction(OpCodes.Ldarg_0));
            list.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Player_Patch), "FixedUpdate")));
            list.Add(new CodeInstruction(OpCodes.Ret));
            return list;
        }

        private static void FixedUpdate(Player __instance)
        {
            float fixedDeltaTime = Time.fixedDeltaTime;
            __instance.UpdateAwake(fixedDeltaTime);
            if (__instance.m_nview.GetZDO() == null)
            {
                return;
            }

            __instance.UpdateTargeted(fixedDeltaTime);
            if (!__instance.m_nview.IsOwner() && !ZNet.instance.IsServer())
            {
                return;
            }

            if (ValheimMP.Instance.DebugShowZDOPlayerLocation.Value && !ZNet.instance.IsServer())
            {
                if (m_debugZdoPlayerLocationObject == null)
                {
                    m_debugZdoPlayerLocationObject = new GameObject();
                    var line = m_debugZdoPlayerLocationObject.AddComponent<LineRenderer>();
                    line.material = Resources.FindObjectsOfTypeAll<Material>().First(k => k.name == "Default-Line");
                    line.widthMultiplier = 0.2f;
                }

                var lineObj = m_debugZdoPlayerLocationObject.GetComponent<LineRenderer>();
                lineObj.SetPosition(0, __instance.m_nview.m_zdo.GetPosition() + new Vector3(0, 2, 0));
                lineObj.SetPosition(1, __instance.m_nview.m_zdo.GetPosition() - new Vector3(0, 0, 0));
            }



            // the server should handle all most of this as well
            if (!__instance.IsDead())
            {
                SyncPlayerMovement(__instance);
                UpdateDelayedDamage(__instance);
                __instance.UpdateEquipQueue(fixedDeltaTime);
                __instance.PlayerAttackInput(fixedDeltaTime);
                __instance.UpdateAttach();
                __instance.UpdateShipControl(fixedDeltaTime);
                __instance.UpdateCrouch(fixedDeltaTime);
                __instance.UpdateDodge(fixedDeltaTime);
                __instance.UpdateStealth(fixedDeltaTime);
                __instance.UpdateStations(fixedDeltaTime);
                __instance.UpdateGuardianPower(fixedDeltaTime);
                __instance.UpdateStats(fixedDeltaTime);
                __instance.UpdateBiome(fixedDeltaTime);
                __instance.UpdateBaseValue(fixedDeltaTime);
                __instance.UpdateCover(fixedDeltaTime);
                __instance.UpdateTeleport(fixedDeltaTime);

                if (ZNet.instance.IsServer())
                {
                    __instance.AutoPickup(fixedDeltaTime);
                    __instance.EdgeOfWorldKill(fixedDeltaTime);

                    __instance.m_nview.GetZDO().Set("stamina", __instance.m_stamina);
                }

                if (!ZNet.instance.IsServer())
                {
                    if (!ValheimMP.Instance.DoNotHideCharacterWhenCameraClose.Value && (bool)GameCamera.instance &&
                        Vector3.Distance(GameCamera.instance.transform.position, __instance.transform.position) < 2f)
                    {
                        __instance.SetVisible(visible: false);
                    }

                    AudioMan.instance.SetIndoor(__instance.InShelter());
                }
            }
        }

        private static void UpdateDelayedDamage(Player __instance)
        {
            if (__instance.m_delayedDamage.Count == 0)
                return;

            var peer = ZNet.instance.GetPeer(__instance.GetPlayerID());

            if (peer == null)
                return;

            while (Time.time + peer.m_rpc.m_averagePing > __instance.m_delayedDamage.Peek().Key)
            {
                __instance.RPC_Damage(0, __instance.m_delayedDamage.Dequeue().Value);
                if (__instance.m_delayedDamage.Count == 0)
                    break;
            }
        }

        [HarmonyPatch(typeof(Player), "CreateTombStone")]
        [HarmonyPrefix]
        private static bool CreateTombStone(Player __instance)
        {
            if (__instance.m_inventory.NrOfItems() != 0)
            {
                __instance.UnequipAllItems();
                GameObject obj = UnityEngine.Object.Instantiate(__instance.m_tombstone, __instance.GetCenterPoint(), __instance.transform.rotation);
                obj.GetComponent<Container>().GetInventory().MoveInventoryToGrave(__instance.m_inventory);
                TombStone component = obj.GetComponent<TombStone>();
                component.Setup(__instance.GetPlayerName(), __instance.GetPlayerID());
            }

            return false;
        }

        [HarmonyPatch(typeof(Player), "OnDeath")]
        [HarmonyPrefix]
        private static bool OnDeath(ref Player __instance)
        {
            // should the client simulate this or only run on the server? I guess we will see the result..
            if (!ZNet.instance.IsServer())
                return false;

            __instance.m_nview.GetZDO().Set("dead", value: true);
            __instance.m_nview.InvokeRPC(ZNetView.Everybody, "OnDeath");

            var peer = ZNet.instance.GetPeer(__instance.GetPlayerID());

            peer.m_playerProfile.m_playerStats.m_deaths++;
            peer.m_playerProfile.SetDeathPoint(__instance.transform.position);
            __instance.CreateDeathEffects();
            __instance.CreateTombStone();
            __instance.m_foods.Clear();
            if (__instance.HardDeath())
            {
                __instance.m_skills.OnDeath();
            }

            ZNetPeer_Patch.SavePeer(peer, false);

            __instance.m_timeSinceDeath = 0f;
            peer.m_respawnWait = ValheimMP.Instance.RespawnDelay.Value;



            if (!__instance.HardDeath())
            {
                __instance.Message(MessageHud.MessageType.TopLeft, "$msg_softdeath");
            }
            __instance.Message(MessageHud.MessageType.Center, "$msg_youdied");

            return false;
        }

        [HarmonyPatch(typeof(Player), "RPC_OnDeath")]
        [HarmonyPostfix]
        private static void RPC_OnDeath(Player __instance, long sender)
        {
            if (__instance == Player.m_localPlayer)
            {
                Game.instance.m_playerProfile.SetDeathPoint(__instance.transform.position);
                __instance.ShowTutorial("death");
                string eventLabel = "biome:" + __instance.GetCurrentBiome();
                Gogan.LogEvent("Game", "Death", eventLabel, 0L);

                Game.instance.RequestRespawn(ValheimMP.Instance.RespawnDelay.Value);
            }
        }

        [HarmonyPatch(typeof(Character), "GetSlideAngle")]
        [HarmonyPostfix]
        private static void GetSlideAngle(Character __instance, ref float __result)
        {
            if (ValheimMP.IsDedicated)
                __result = 90f;
        }

        internal static void RPC_SyncPlayerMovement(ZRpc rpc, ZPackage pkg)
        {
            var peer = ZNet.instance.GetPeer(rpc);
            var player = peer.m_player;
            Vector3 movedir = pkg.ReadVector3();
            Vector3 clientPosition = pkg.ReadVector3();
            Vector3 lookdir = pkg.ReadVector3();

            var run = false;
            var walk = false;
            var movedirSpeed = movedir.sqrMagnitude;
            if (movedirSpeed > 1.1)
            {
                run = true;
            }
            else if (movedirSpeed > 0.5)
            {
            }
            else if (movedirSpeed > 0.1)
            {
                walk = true;
            }

            movedir.Normalize();

            if (movedirSpeed > 0)
            {
                player.AttachStop();
                player.StopEmote();
            }

            player.m_walk = walk;
            player.m_run = run;
            player.m_clientPosition = clientPosition;
            player.m_lookDir = lookdir;
            player.m_moveDir = movedir;
            player.m_lastMoveDir = player.m_moveDir;
        }

        private static void SyncPlayerMovement(Player __instance)
        {
            if (!ZNet.instance.IsServer())
            {
                if (__instance.IsOwner() && Time.time - m_lastSyncTime > m_playerTickRate)
                {
                    var movedir = __instance.m_moveDir;
                    movedir.Normalize();

                    m_lastSyncTime = Time.time;

                    var pkg = new ZPackage();
                    var moveSpeed = 1.0f;
                    if (__instance.m_run || __instance.m_running) moveSpeed *= 2f;
                    if (__instance.m_walk) moveSpeed *= 0.5f;
                    pkg.Write(movedir * moveSpeed);
                    pkg.Write(__instance.transform.position);
                    pkg.Write(__instance.m_lookDir);

                    // Don't use routed rpc, there is minor overhead and since we call this very frequently we can save a little calling the rpc directly.
                    var rpc = ZNet.instance.GetServerRPC();
                    rpc.Invoke("SyncPlayerMovement", pkg);
                }
            }
            else
            {
                if (__instance.m_clientPosition != Vector3.zero)
                {
                    var sqrDist = (__instance.m_clientPosition - __instance.transform.position).sqrMagnitude;
                    if ((sqrDist > 0.25f) || (sqrDist > 0.1f && __instance.m_lastMoveDir == Vector3.zero))
                    {
                        // The server has gone out of sync with the client
                        // Instead of following the normal move dir; move toward where the client is.
                        // (This is something that will happen constantly, and is quite normal but needed for compensation since we dont sync pos\rot every frame)
                        __instance.m_moveDir = __instance.m_clientPosition - __instance.transform.position;
                        __instance.m_moveDir.Normalize();
                        __instance.m_lastMoveFlags = 1;
                    }
                    else
                    {
                        __instance.m_clientPosition = Vector3.zero;
                        __instance.m_moveDir = __instance.m_lastMoveDir;
                        __instance.m_lastMoveFlags = 0;
                    }
                }
            }
        }

        //[HarmonyPatch(typeof(Character), "UpdateWalking")]
        //[HarmonyPostfix]
        //static void UpdateWalking(ref Character __instance, float dt)
        //{
        //    if (ZNet.instance.IsServer() && __instance is Player player)
        //    {
        //        if (player.m_clientPosition != Vector3.zero)
        //        {
        //            var diffDir = player.m_clientPosition - player.transform.position;
        //            var diffDist = diffDir.sqrMagnitude;
        //            diffDir.Normalize();
        //            diffDir.y *= 0.2f;
        //            if (diffDist > 0.05f && (player.m_moveDir == Vector3.zero || Vector3.Dot(diffDir, player.m_moveDir) > 0.5f))
        //            {
        //                __instance.m_body.AddForce(diffDir.normalized * __instance.m_walkSpeed, ForceMode.Acceleration);
        //            }
        //        }
        //    }
        //}


        private enum EAttackMode : int
        {
            Attack = 0,
            Secondary = 1,
            DrawAttack = 2
        }

        [HarmonyPatch(typeof(Player), "PlayerAttackInput")]
        [HarmonyPrefix]
        private static bool PlayerAttackInput(Player __instance, float dt)
        {
            if (__instance.InPlaceMode())
            {
                return false;
            }

            if (!ZNet.instance.IsServer())
            {
                if (__instance.m_blocking != m_isBlocking)
                {
                    m_isBlocking = __instance.m_blocking;
                    if (__instance.m_blocking) __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "StartBlocking");
                    else __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "StopBlocking");
                }
            }

            ItemDrop.ItemData currentWeapon = __instance.GetCurrentWeapon();
            if (currentWeapon != null && currentWeapon.m_shared.m_holdDurationMin > 0f)
            {
                if (__instance.m_blocking || __instance.InMinorAction())
                {
                    __instance.m_attackDrawTime = -1f;
                    if (!string.IsNullOrEmpty(currentWeapon.m_shared.m_holdAnimationState))
                    {
                        __instance.m_zanim.SetBool(currentWeapon.m_shared.m_holdAnimationState, value: false);
                    }
                    return false;
                }
                bool flag = currentWeapon.m_shared.m_holdStaminaDrain <= 0f || __instance.HaveStamina();
                if (__instance.m_attackDrawTime < 0f)
                {
                    if (!__instance.m_attackDraw)
                    {
                        __instance.m_attackDrawTime = 0f;
                        if (!ZNet.instance.IsServer()) __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "CancelDrawAttack");
                    }
                }
                else if (__instance.m_attackDraw && flag && __instance.m_attackDrawTime >= 0f)
                {
                    if (__instance.m_attackDrawTime == 0f)
                    {
                        if (!currentWeapon.m_shared.m_attack.StartDraw(__instance, currentWeapon))
                        {
                            __instance.m_attackDrawTime = -1f;
                            return false;
                        }

                        if (!ZNet.instance.IsServer()) __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "StartDrawAttack");
                        currentWeapon.m_shared.m_holdStartEffect.Create(__instance.transform.position, Quaternion.identity, __instance.transform);
                    }
                    __instance.m_attackDrawTime += dt;
                    if (!string.IsNullOrEmpty(currentWeapon.m_shared.m_holdAnimationState))
                    {
                        __instance.m_zanim.SetBool(currentWeapon.m_shared.m_holdAnimationState, value: true);
                    }
                    __instance.UseStamina(currentWeapon.m_shared.m_holdStaminaDrain * dt);
                }
                else if (__instance.m_attackDrawTime > 0f)
                {
                    if (flag)
                    {
                        __instance.StartAttack(null, secondaryAttack: false);
                        if (!ZNet.instance.IsServer()) __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "ReleaseDrawAttack");
                    }
                    else
                    {
                        if (!ZNet.instance.IsServer()) __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "CancelDrawAttack");
                    }

                    if (!string.IsNullOrEmpty(currentWeapon.m_shared.m_holdAnimationState))
                    {
                        __instance.m_zanim.SetBool(currentWeapon.m_shared.m_holdAnimationState, value: false);
                    }
                    __instance.m_attackDrawTime = 0f;
                }
            }
            else
            {
                if (__instance.m_attack)
                {
                    __instance.m_queuedAttackTimer = 0.5f;
                    __instance.m_queuedSecondAttackTimer = 0f;
                }
                if (__instance.m_secondaryAttack)
                {
                    __instance.m_queuedSecondAttackTimer = 0.5f;
                    __instance.m_queuedAttackTimer = 0f;
                }
                __instance.m_queuedAttackTimer -= dt;
                __instance.m_queuedSecondAttackTimer -= dt;
                if (__instance.m_queuedAttackTimer > 0f && __instance.StartAttack(null, secondaryAttack: false))
                {
                    if (!ZNet.instance.IsServer()) __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "StartAttack");
                    __instance.m_queuedAttackTimer = 0f;
                }
                if (__instance.m_queuedSecondAttackTimer > 0f && __instance.StartAttack(null, secondaryAttack: true))
                {
                    if (!ZNet.instance.IsServer()) __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "StartSecondaryAttack");
                    __instance.m_queuedSecondAttackTimer = 0f;
                }
            }
            return false;
        }

        private static void RPC_StartBlocking(Player __instance, long sender)
        {
            if (__instance.GetPlayerID() != sender || __instance.IsDead())
                return;

            __instance.m_attack = false;
            __instance.m_secondaryAttack = false;
            __instance.m_attackDraw = false;

            __instance.m_blocking = true;
            __instance.PlayerAttackInput(0.0001f);
        }

        private static void RPC_StopBlocking(Player __instance, long sender)
        {
            if (__instance.GetPlayerID() != sender || __instance.IsDead())
                return;

            __instance.m_blocking = false;
        }

        private static void RPC_StartAttack(Player __instance, long sender)
        {
            if (__instance.GetPlayerID() != sender || __instance.IsDead())
                return;

            __instance.m_blocking = false;
            __instance.m_attack = false;
            __instance.m_secondaryAttack = false;
            __instance.m_attackDraw = false;

            __instance.m_attack = true;
            __instance.PlayerAttackInput(0.0001f);
            __instance.m_attack = false;
        }

        private static void RPC_StartSecondaryAttack(Player __instance, long sender)
        {
            if (__instance.GetPlayerID() != sender || __instance.IsDead())
                return;

            __instance.m_blocking = false;
            __instance.m_attack = false;
            __instance.m_secondaryAttack = false;
            __instance.m_attackDraw = false;

            __instance.m_secondaryAttack = true;
            __instance.PlayerAttackInput(0.0001f);
            __instance.m_secondaryAttack = false;

        }

        private static void RPC_StartDrawAttack(Player __instance, long sender)
        {
            if (__instance.GetPlayerID() != sender || __instance.IsDead())
                return;

            __instance.m_blocking = false;
            __instance.m_attack = false;
            __instance.m_secondaryAttack = false;
            __instance.m_attackDraw = false;

            __instance.m_attackDraw = true;
            __instance.PlayerAttackInput(0.0001f);
        }


        private static void RPC_ReleaseDrawAttack(Player __instance, long sender)
        {
            if (__instance.GetPlayerID() != sender || __instance.IsDead())
                return;

            __instance.m_attackDraw = false;
            __instance.PlayerAttackInput(0.0001f);
        }

        private static void RPC_CancelDrawAttack(Player __instance, long sender)
        {
            if (__instance.GetPlayerID() != sender || __instance.IsDead())
                return;

            __instance.m_attackDrawTime = 0f;
            __instance.m_attackDraw = false;

            ItemDrop.ItemData currentWeapon = __instance.GetCurrentWeapon();
            if (currentWeapon != null &&
                currentWeapon.m_shared.m_holdDurationMin > 0f &&
                !string.IsNullOrEmpty(currentWeapon.m_shared.m_holdAnimationState))
            {
                __instance.m_zanim.SetBool(currentWeapon.m_shared.m_holdAnimationState, value: false);
            }
        }

        [HarmonyPatch(typeof(Player), "OnJump")]
        [HarmonyPostfix]
        private static void OnJump(Player __instance)
        {
            if (__instance == Player.m_localPlayer)
            {
                __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "Jump");
            }
        }

        private static void RPC_Jump(Player __instance, long sender)
        {
            if (__instance.GetPlayerID() != sender)
                return;

            __instance.Jump();
        }

        [HarmonyPatch(typeof(Player), "UpdateDodge")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> UpdateDodge(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(AccessTools.Method(typeof(Humanoid), "AbortEquipQueue")))
                {
                    list.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Player_Patch), "Dodge",
                            new Type[] { typeof(Player) }))

                    });
                    break;
                }
            }

            return list.AsEnumerable();
        }

        private static void Dodge(Player __instance)
        {
            if (__instance == Player.m_localPlayer)
            {
                __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "Dodge", __instance.m_queuedDodgeDir);
            }
        }

        private static void RPC_Dodge(Player __instance, long sender, Vector3 dodgeDir)
        {
            if (__instance.GetPlayerID() != sender)
                return;
            __instance.Dodge(dodgeDir);
            __instance.UpdateDodge(0.0001f);
        }

        [HarmonyPatch(typeof(Player), "SetCrouch")]
        [HarmonyPrefix]
        private static bool SetCrouch(Player __instance, bool crouch)
        {
            if (__instance.m_crouchToggled != crouch)
            {
                __instance.m_crouchToggled = crouch;
                if (!ValheimMP.IsDedicated)
                {
                    __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "Crouch", crouch);
                }
            }

            return false;
        }

        private static void RPC_Crouch(Player __instance, long sender, bool crouch)
        {
            if (__instance.GetPlayerID() != sender)
                return;

            __instance.SetCrouch(crouch);
        }


        private static void RPC_ConsumeItem(Player __instance, long sender, int itemId, ZDOID inventoryId)
        {
            if (__instance.GetPlayerID() != sender)
                return;

            var inventory = inventoryId.IsNone() ? __instance.m_inventory : ValheimMP.Instance.InventoryManager.GetInventory(inventoryId);

            if (inventory == null)
            {
                ValheimMP.Log($"RPC_ConsumeItem itemId: {itemId} inventoryId: {inventoryId} Inventory not found.");
                return;
            }

            var item = inventory.m_inventory.Single(k => k.m_id == itemId);

            if (item == null)
                return;

            if (__instance.ConsumeItem(inventory, item))
            {
                __instance.m_nview.InvokeRPC("ClientConsume", item.m_dropPrefab.name.GetStableHashCode());
            }
        }

        private static void RPC_ClientConsume(Player __instance, int itemHash)
        {
            var itemObj = ObjectDB.instance.GetItemPrefab(itemHash);
            if (itemObj == null)
                return;

            // Have ourselves a dummy object to eat.
            // We could eat the item actually being eaten but since the order of networking shouldn't be reliable (well it is, but I like to pretend it isn't)
            // So our inventory item can already be destroyed by the time we try to eat it, so instead we create a dummy to eat.
            ZNetView.m_forceDisableInit = true;
            GameObject gameObject = UnityEngine.Object.Instantiate(itemObj);
            ZNetView.m_forceDisableInit = false;

            var itemDrop = gameObject.GetComponent<ItemDrop>();
            if (itemDrop == null)
                return;
            var item = itemDrop.m_itemData;
            if (item == null)
                return;

            if (item.m_shared.m_consumeStatusEffect)
            {
                __instance.m_seman.AddStatusEffect(item.m_shared.m_consumeStatusEffect, resetTime: true);
            }
            if (item.m_shared.m_food > 0f)
            {
                __instance.EatFood(item);
            }
            __instance.m_consumeItemEffects.Create(Player.m_localPlayer.transform.position, Quaternion.identity);
            __instance.m_zanim.SetTrigger("eat");

            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [HarmonyPatch(typeof(Player), "ConsumeItem")]
        [HarmonyPrefix]
        private static bool ConsumeItem(ref Player __instance, ref bool __result, Inventory inventory, ItemDrop.ItemData item)
        {
            if (ZNet.instance == null || ZNet.instance.IsServer())
                return true;

            if (!__instance.CanConsumeItem(item))
            {
                __result = false;
                return false;
            }


            var inventoryId = (inventory.m_nview != null && inventory.m_nview.m_zdo != null) ? inventory.m_nview.m_zdo.m_uid : ZDOID.None;

            __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "ConsumeItem", item.m_id, inventoryId);
            __result = false;
            return false;
        }

        [HarmonyPatch(typeof(Player), "SetCraftingStation")]
        [HarmonyPostfix]
        private static void SetCraftingStation(ref Player __instance)
        {
            if (ZNet.instance.IsServer())
            {
                return;
            }

            var stationId = ZDOID.None;

            if (__instance.m_currentStation != null &&
                __instance.m_currentStation.m_nview != null &&
                __instance.m_currentStation.m_nview.m_zdo != null)
            {
                stationId = __instance.m_currentStation.m_nview.m_zdo.m_uid;
            }

            __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "SetCraftingStation", stationId);
        }

        private static void RPC_SetCraftingStation(Player __instance, long sender, ZDOID stationId)
        {
            if (__instance.GetPlayerID() != sender)
                return;

            var station = ZNetScene.instance.FindInstance(stationId);
            if (station != null)
            {
                __instance.m_currentStation = station.GetComponent<CraftingStation>();
            }
        }

        [HarmonyPatch(typeof(Player), "TeleportTo")]
        [HarmonyPrefix]
        private static bool TeleportTo(Player __instance, ref bool __result, Vector3 pos, Quaternion rot, bool distantTeleport)
        {
            __result = false;

            if (__instance.IsTeleporting())
            {
                return false;
            }

            if (__instance.m_teleportCooldown < 2f)
            {
                return false;
            }

            var peer = ZNet.instance.GetPeer(__instance.GetOwner());
            if (peer != null)
            {
                peer.m_refPos = __instance.transform.position;
            }

            __instance.m_nview.InvokeRPC("TeleportTo", pos, rot, distantTeleport);

            __instance.m_teleporting = true;
            __instance.m_distantTeleport = distantTeleport;
            __instance.m_teleportTimer = 0f;
            __instance.m_teleportCooldown = 0f;
            __instance.m_teleportFromPos = __instance.transform.position;
            __instance.m_teleportFromRot = __instance.transform.rotation;
            __instance.m_teleportTargetPos = pos;
            __instance.m_teleportTargetRot = rot;

            __result = true;
            return false;
        }

        private static void RPC_TeleportTo(Player __instance, Vector3 pos, Quaternion rot, bool distantTeleport)
        {
            Hud.instance.m_loadingScreen.alpha = 1f;
            ZNet.instance.SetReferencePosition(pos);
            __instance.m_teleporting = true;
            __instance.m_distantTeleport = distantTeleport;
            __instance.m_teleportTimer = 0f;
            __instance.m_teleportCooldown = 0f;
            __instance.m_teleportFromPos = pos;
            __instance.m_teleportFromRot = rot;
            __instance.m_teleportTargetPos = pos;
            __instance.m_teleportTargetRot = rot;
        }

        [HarmonyPatch(typeof(Player), "UpdateTeleport")]
        [HarmonyPrefix]
        private static bool UpdateTeleport(Player __instance, float dt)
        {
            if (!__instance.m_teleporting)
            {
                __instance.m_teleportCooldown += dt;
                return false;
            }

            if (ValheimMP.IsDedicated)
            {
                var peer = ZNet.instance.GetPeer(__instance.GetOwner());

                if (peer == null)
                {
                    ValheimMP.Log("No peer?");
                    __instance.m_teleporting = false;
                    return false;
                }

                peer.m_refPos = __instance.m_teleportTargetPos;
            }

            __instance.m_teleportCooldown = 0f;
            __instance.m_teleportTimer += dt;

            Vector3 lookDir = __instance.m_teleportTargetRot * Vector3.forward;
            __instance.transform.position = __instance.m_teleportTargetPos;
            __instance.transform.rotation = __instance.m_teleportTargetRot;
            __instance.m_body.velocity = Vector3.zero;
            __instance.m_maxAirAltitude = __instance.transform.position.y;
            __instance.SetLookDir(lookDir);

            if (!ZNetScene.instance.IsAreaReady(__instance.m_teleportTargetPos))
            {
                return false;
            }

            if (ValheimMP.IsDedicated)
            {
                if (ZoneSystem.instance.FindFloor(__instance.m_teleportTargetPos, out var height))
                {
                    __instance.m_teleportTimer = 0f;
                    __instance.m_teleporting = false;
                    __instance.ResetCloth();
                }
                else
                {
                    if (__instance.m_distantTeleport)
                    {
                        Vector3 position = __instance.transform.position;
                        position.y = ZoneSystem.instance.GetSolidHeight(__instance.m_teleportTargetPos) + 0.5f;
                        __instance.transform.position = position;
                    }
                    else
                    {
                        __instance.transform.rotation = __instance.m_teleportFromRot;
                        __instance.transform.position = __instance.m_teleportFromPos;
                        __instance.m_maxAirAltitude = __instance.transform.position.y;
                        __instance.Message(MessageHud.MessageType.Center, "$msg_portal_blocked");
                    }
                    __instance.m_teleportTimer = 0f;
                    __instance.m_teleporting = false;
                    __instance.ResetCloth();
                }
            }
            else
            {
                if (__instance.m_teleportTimer > 2f && !ZNetScene_Patch.m_isStillLoading)
                {
                    __instance.m_teleportTimer = 0f;
                    __instance.m_teleporting = false;
                    __instance.ResetCloth();
                }
            }

            return false;
        }

        [HarmonyPatch(typeof(Player), "AutoPickup")]
        [HarmonyPrefix]
        private static bool AutoPickup(Player __instance, float dt)
        {
            if (!ZNet.instance.IsServer())
                return false;
            return PrivateArea_Patch.CheckAccess(__instance.GetPlayerID(), __instance.transform.position, 0, false);
        }

        [HarmonyPatch(typeof(Player), "RemovePiece")]
        [HarmonyPrefix]
        private static bool RemovePiece(Player __instance, ref bool __result)
        {
            __result = false;
            if (Physics.Raycast(GameCamera.instance.transform.position, GameCamera.instance.transform.forward, out var hitInfo, 50f, __instance.m_removeRayMask) && Vector3.Distance(hitInfo.point, __instance.m_eye.position) < __instance.m_maxPlaceDistance)
            {
                Piece piece = hitInfo.collider.GetComponentInParent<Piece>();
                if (piece == null && (bool)hitInfo.collider.GetComponent<Heightmap>())
                {
                    piece = TerrainModifier.FindClosestModifierPieceInRange(hitInfo.point, 2.5f);
                }
                if ((bool)piece)
                {
                    if (!piece.m_canBeRemoved)
                    {
                        return false;
                    }
                    if (Location.IsInsideNoBuildLocation(piece.transform.position))
                    {
                        __instance.Message(MessageHud.MessageType.Center, "$msg_nobuildzone");
                        return false;
                    }
                    if (!PrivateArea.CheckAccess(piece.transform.position))
                    {
                        __instance.Message(MessageHud.MessageType.Center, "$msg_privatezone");
                        return false;
                    }
                    if (!__instance.CheckCanRemovePiece(piece))
                    {
                        return false;
                    }
                    ZNetView component = piece.GetComponent<ZNetView>();
                    if (component == null)
                    {
                        return false;
                    }
                    if (!piece.CanBeRemoved())
                    {
                        __instance.Message(MessageHud.MessageType.Center, "$msg_cantremovenow");
                        return false;
                    }

                    var nv = piece.GetComponent<ZNetView>();
                    if (nv == null)
                    {
                        return false;
                    }

                    // TODO: We only delete a bit and insert this here, should make a transpiler
                    __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "RemovePiece", nv.GetZDO().m_uid);

                    ItemDrop.ItemData rightItem = __instance.GetRightItem();
                    if (rightItem != null)
                    {
                        __instance.FaceLookDirection();
                        __instance.m_zanim.SetTrigger(rightItem.m_shared.m_attack.m_attackAnimation);
                    }
                    __result = true;
                    return false;
                }
            }
            return false;
        }

        private static void RPC_RemovePiece(Player __instance, long sender, ZDOID id)
        {
            if (__instance.GetPlayerID() != sender)
                return;

            var go = ZNetScene.instance.FindInstance(id);
            if (go == null)
                return;

            var piece = go.GetComponent<Piece>();
            if (piece == null)
                return;

            if (!piece.m_canBeRemoved)
                return;

            if (Vector3.Distance(go.transform.position, __instance.transform.position) > __instance.m_maxPlaceDistance)
                return;

            if (Location.IsInsideNoBuildLocation(piece.transform.position))
                return;

            if (!__instance.CheckCanRemovePiece(piece))
                return;

            if (!PrivateArea_Patch.CheckAccess(sender, go.transform.position))
                return;


            WearNTear component2 = piece.GetComponent<WearNTear>();
            if ((bool)component2)
            {
                component2.Remove();
            }
            else
            {
                piece.DropResources();
                piece.m_placeEffect.Create(piece.transform.position, piece.transform.rotation, piece.gameObject.transform);
                __instance.m_removeEffects.Create(piece.transform.position, Quaternion.identity);
                ZNetScene.instance.Destroy(piece.gameObject);
            }
        }

        [HarmonyPatch(typeof(Player), "Repair")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Repair(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(AccessTools.Method(typeof(Player), "FaceLookDirection")))
                {
                    list.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldloc_0),
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Player_Patch), "ServerRepair"))
                    });
                    break;
                }
            }
            return list;
        }

        private static void ServerRepair(Player __instance, Piece hoveringPiece)
        {
            __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "RepairPiece",
                hoveringPiece.GetComponent<ZNetView>().GetZDO().m_uid);
        }

        private static void RPC_RepairPiece(Player __instance, long sender, ZDOID id)
        {
            if (__instance.GetPlayerID() != sender)
                return;

            var go = ZNetScene.instance.FindInstance(id);
            if (go == null)
                return;

            var piece = go.GetComponent<Piece>();
            if (piece == null)
                return;

            if (Vector3.Distance(go.transform.position, __instance.transform.position) > __instance.m_maxPlaceDistance)
                return;

            if (!__instance.CheckCanRemovePiece(piece))
                return;

            if (Location.IsInsideNoBuildLocation(piece.transform.position))
                return;

            if (!PrivateArea_Patch.CheckAccess(sender, go.transform.position))
                return;

            WearNTear component = piece.GetComponent<WearNTear>();
            if ((bool)component && component.Repair())
            {
                var toolItem = __instance.GetRightItem();
                __instance.FaceLookDirection();
                __instance.m_zanim.SetTrigger(toolItem.m_shared.m_attack.m_attackAnimation);
                piece.m_placeEffect.Create(piece.transform.position, piece.transform.rotation);

                __instance.UseStamina(toolItem.m_shared.m_attack.m_attackStamina);
                if (toolItem.m_shared.m_useDurability)
                {
                    toolItem.m_durability -= toolItem.m_shared.m_useDurabilityDrain;
                }
            }
        }

        [HarmonyPatch(typeof(Player), "AddKnownText")]
        [HarmonyPrefix]
        private static void AddKnownText(Player __instance, string label, string text)
        {
            if (!string.IsNullOrWhiteSpace(label) && !ZNet.instance.IsServer() && !__instance.m_knownTexts.ContainsKey(label))
            {
                __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "AddKnownText", label, text);
            }
        }

        private static void RPC_AddKnownText(Player __instance, long sender, string label, string text)
        {
            if (__instance.GetPlayerID() != sender)
                return;

            __instance.AddKnownText(label, text);
        }

        [HarmonyPatch(typeof(Player), "SetSeenTutorial")]
        [HarmonyPrefix]
        private static void SetSeenTutorial(Player __instance, string name)
        {
            if (!string.IsNullOrWhiteSpace(name) && !__instance.m_shownTutorials.Contains(name) && !ZNet.instance.IsServer())
            {
                __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "SetSeenTutorial", name);
            }
        }

        private static void RPC_SetSeenTutorial(Player __instance, long sender, string name)
        {
            if (__instance.GetPlayerID() != sender)
                return;

            __instance.SetSeenTutorial(name);
        }

        [HarmonyPatch(typeof(Humanoid), "HideHandItems")]
        [HarmonyPostfix]
        private static void HideHandItems(Humanoid __instance)
        {
            if (!ZNet.instance.IsServer() && __instance.IsPlayer())
            {
                __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "HideHandItems");
            }
        }

        private static void RPC_HideHandItems(Player __instance, long sender)
        {
            __instance.HideHandItems();
        }

        [HarmonyPatch(typeof(Player), "StartEmote")]
        [HarmonyPrefix]
        private static void StartEmote(Player __instance, string emote, bool oneshot = true)
        {
            if (!ZNet.instance.IsServer())
            {
                __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "StartEmote", emote, oneshot);
            }
        }

        private static void RPC_StartEmote(Player __instance, long sender, string emote, bool oneshot)
        {
            if (__instance.GetPlayerID() != sender)
                return;

            __instance.StartEmote(emote, oneshot);
        }

        [HarmonyPatch(typeof(Player), "StopEmote")]
        [HarmonyPrefix]
        private static void StopEmote(Player __instance)
        {
            if (!ZNet.instance.IsServer() && __instance.m_nview.GetZDO().GetString("emote") != "")
            {
                __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "StopEmote");
            }
        }

        private static void RPC_StopEmote(Player __instance, long sender)
        {
            if (__instance.GetPlayerID() != sender)
                return;

            __instance.StopEmote();
        }

        [HarmonyPatch(typeof(Player), "AttachStart")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> AttachStartInject(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].StoresField(AccessTools.Field(typeof(Player), "m_attached")))
                {
                    list.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldarg_1),
                        new CodeInstruction(OpCodes.Ldarg_S, 4),
                        new CodeInstruction(OpCodes.Ldarg_S, 5),
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Player_Patch), "AttachStart",
                            new Type[] { typeof(Player), typeof(Transform), typeof(string), typeof(Vector3) }))

                    });
                    break;
                }
            }

            return list.AsEnumerable();
        }

        private static void AttachStart(Player __instance, Transform attachPoint, string attachAnimation, Vector3 detachOffset)
        {
            if (ZNet.instance.IsServer())
            {
                var nv = attachPoint.GetComponentInParent<ZNetView>();
                if (nv != null && nv.m_zdo != null)
                {
                    var zdo = __instance.m_nview.GetZDO();
                    zdo.Set(ZSyncTransform.m_parentIDHash, nv.m_zdo.m_uid);
                    zdo.Set("m_attachPoint", attachPoint.gameObject.GetFullName(nv.gameObject));
                    zdo.Set("m_attachAnimation", attachAnimation);
                    zdo.Set("m_detachOffset", detachOffset);
                }
            }
        }

        [HarmonyPatch(typeof(Player), "AttachStop")]
        [HarmonyPostfix]
        private static void AttachStop(Player __instance)
        {
            if (ZNet.instance.IsServer() && __instance.m_attached == false)
            {
                __instance.m_nview.GetZDO().Set("m_attachPoint", "");
            }
        }

        private static void ZDOEvent_Attach(Player __instance)
        {
            if (__instance == null)
                return;

            var zdo = __instance.m_nview.m_zdo;
            var attachObjectID = zdo.GetZDOID(ZSyncTransform.m_parentIDHash);
            var attachPoint = zdo.GetString("m_attachPoint");

            var attachObject = ZNetScene.instance.FindInstance(attachObjectID);
            if (!string.IsNullOrWhiteSpace(attachPoint) && attachObject != null)
            {
                var attachTransform = attachObject.transform.Find(attachPoint);
                ValheimMP.Log($"Find {attachPoint} == {attachTransform}");
                if (attachTransform != null)
                {
                    __instance.m_attached = true;
                    __instance.m_attachPoint = attachTransform;
                    __instance.m_attachAnimation = zdo.GetString("m_attachAnimation");
                    __instance.m_detachOffset = zdo.GetVec3("m_detachOffset", Vector3.zero);
                    __instance.m_zanim.SetBool(__instance.m_attachAnimation, value: true);
                    return;
                }
            }

            __instance.AttachStop();
        }

        [HarmonyPatch(typeof(Player), "IsPVPEnabled")]
        [HarmonyPrefix]
        private static bool IsPVPEnabled(Player __instance, ref bool __result)
        {
            if (__instance.m_nview.m_zdo.GetBool("forcedpvp", false))
            {
                __result = true;
                return false;
            }

            return true;
        }
        [HarmonyPatch(typeof(Player), "SetPVP")]
        [HarmonyPrefix]
        private static bool SetPVP(Player __instance)
        {
            return ValheimMP.IsDedicated || __instance.CanSwitchPVP();
        }

        [HarmonyPatch(typeof(Player), "CanSwitchPVP")]
        [HarmonyPrefix]
        private static bool CanSwitchPVP(Player __instance, ref bool __result)
        {
            if (__instance.m_nview.m_zdo.GetBool("forcedpvp", false))
            {
                __result = false;
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(Player), "UpdateBiome")]
        [HarmonyPrefix]
        private static void UpdateBiome(Player __instance, float dt)
        {
            if (__instance.m_biomeTimer + dt > 1f)
            {
                TestForcePVP(__instance);
            }
        }

        private static void TestForcePVP(Player __instance)
        {
            var vmp = ValheimMP.Instance;
            var biomeDistance = vmp.ForcedPVPDistanceForBiomesOnly.Value;
            biomeDistance *= biomeDistance;
            var centerDistance = vmp.ForcedPVPDistanceFromCenter.Value;
            centerDistance *= centerDistance;
            var playerDistance = __instance.transform.position.sqrMagnitude;

            var forcedpvp =
                playerDistance > centerDistance ||
                (vmp.ForcedPVPBiomes.TryGetValue(__instance.m_currentBiome, out var configEntry)
                && configEntry.Value && playerDistance > biomeDistance);

            if (__instance.m_nview)
            {
                __instance.m_nview.m_zdo?.Set("forcedpvp", forcedpvp);
            }
        }
    }


}

