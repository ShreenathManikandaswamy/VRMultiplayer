using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace XRMultiplayer.Editor
{
    public static class PPEAvatarRigSetupEditor
    {
        const string k_DefaultPrefabPath = "Assets/Avatar/PlayerPrefabs/PPE_Network_Player_Avatar.prefab";
        const string k_TraineePrefabPath = "Assets/Avatar/PlayerPrefabs/PPE_Network_Player_Avatar_Trainee.prefab";
        const string k_SafetyOfficerPrefabPath = "Assets/Avatar/PlayerPrefabs/PPE_Network_Player_Avatar_SafetyOfficer.prefab";
        const string k_LocomotionControllerPath = "Assets/Avatar/Animator/PPEAvatar_Locomotion.controller";

        [MenuItem("XR Multiplayer/Avatar/Setup PPE Humanoid Rig")]
        public static void SetupPrefab()
        {
            SetupPrefabAtPath(k_DefaultPrefabPath, "Ch33_nonPBR", "Ch31_nonPBR", "Ch33", "Ch31", "char02_1");
        }

        [MenuItem("XR Multiplayer/Avatar/Setup Trainee Avatar Rig")]
        public static void SetupTraineePrefab()
        {
            SetupPrefabAtPath(k_TraineePrefabPath, "Ch31_nonPBR", "Ch31", "Ch33_nonPBR", "Ch33", "char02_1");
        }

        [MenuItem("XR Multiplayer/Avatar/Setup Safety Officer Avatar Rig")]
        public static void SetupSafetyOfficerPrefab()
        {
            SetupPrefabAtPath(k_SafetyOfficerPrefabPath, "Ch33_nonPBR", "Ch33", "Ch31_nonPBR", "Ch31", "char02_1");
        }

        static void SetupPrefabAtPath(string prefabPath, params string[] preferredAvatarNames)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                SetupPrefabContents(root, preferredAvatarNames);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"Configured humanoid rig on {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void SetupPrefabContents(GameObject root, string[] preferredAvatarNames)
        {
            Transform avatar = FindPreferredAvatar(root.transform, preferredAvatarNames);
            if (avatar == null)
                throw new MissingReferenceException("Could not find a supported humanoid avatar under PPE_Network_Player_Avatar.");

            Animator animator = avatar.GetComponent<Animator>();
            if (animator == null)
                animator = avatar.gameObject.AddComponent<Animator>();

            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(k_LocomotionControllerPath);
            if (controller != null)
                animator.runtimeAnimatorController = controller;

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            RigBuilder rigBuilder = avatar.GetComponent<RigBuilder>();
            if (rigBuilder == null)
                rigBuilder = avatar.gameObject.AddComponent<RigBuilder>();

            Transform rigRoot = GetOrCreateChild(avatar, "AvatarRig");
            Transform rigObject = GetOrCreateChild(rigRoot, "Rig");
            Transform targetsRoot = GetOrCreateChild(rigRoot, "IKTargets");
            Transform hintsRoot = GetOrCreateChild(rigRoot, "IKHints");

            Rig rig = rigObject.GetComponent<Rig>();
            if (rig == null)
                rig = rigObject.gameObject.AddComponent<Rig>();

            Transform headTarget = GetOrCreateChild(targetsRoot, "HeadTarget");
            Transform leftHandTarget = GetOrCreateChild(targetsRoot, "LeftHandTarget");
            Transform rightHandTarget = GetOrCreateChild(targetsRoot, "RightHandTarget");
            Transform leftElbowHint = GetOrCreateChild(hintsRoot, "LeftElbowHint");
            Transform rightElbowHint = GetOrCreateChild(hintsRoot, "RightElbowHint");

            AvatarBoneSet bones = ResolveBones(avatar);

            CopyPose(headTarget, bones.head != null ? bones.head : avatar);
            CopyPose(leftHandTarget, bones.leftHand);
            CopyPose(rightHandTarget, bones.rightHand);
            PositionElbowHint(leftElbowHint, bones.leftUpperArm, bones.leftLowerArm, -0.28f);
            PositionElbowHint(rightElbowHint, bones.rightUpperArm, bones.rightLowerArm, 0.28f);

            ConfigureArmIK(GetOrCreateChild(rigObject, "LeftArmIK"), bones.leftUpperArm, bones.leftLowerArm, bones.leftHand, leftHandTarget, leftElbowHint);
            ConfigureArmIK(GetOrCreateChild(rigObject, "RightArmIK"), bones.rightUpperArm, bones.rightLowerArm, bones.rightHand, rightHandTarget, rightElbowHint);

            ConfigureRigBuilder(rigBuilder, rig);
            ConfigureBinder(root, avatar, headTarget, leftHandTarget, rightHandTarget);
            ConfigureLocomotionDriver(root, avatar, animator);
            ConfigureLocalOwnerVisibility(root, avatar);
            ConfigurePPEAvatarSetup(root, avatar, bones);
            SetAvatarVisualsEnabled(avatar, true);
            DisableOtherAvatarVisuals(root.transform, avatar);

            EditorUtility.SetDirty(root);
        }

        static void ConfigureArmIK(Transform constraintRoot, Transform root, Transform mid, Transform tip, Transform target, Transform hint)
        {
            TwoBoneIKConstraint constraint = constraintRoot.GetComponent<TwoBoneIKConstraint>();
            if (constraint == null)
                constraint = constraintRoot.gameObject.AddComponent<TwoBoneIKConstraint>();

            TwoBoneIKConstraintData data = constraint.data;
            data.root = root;
            data.mid = mid;
            data.tip = tip;
            data.target = target;
            data.hint = hint;
            data.targetPositionWeight = 1.0f;
            data.targetRotationWeight = 1.0f;
            data.hintWeight = 0.75f;
            data.maintainTargetPositionOffset = false;
            data.maintainTargetRotationOffset = false;
            constraint.data = data;
            constraint.weight = 1.0f;
        }

        static void ConfigureRigBuilder(RigBuilder rigBuilder, Rig rig)
        {
            SerializedObject serializedRigBuilder = new(rigBuilder);
            SerializedProperty layers = serializedRigBuilder.FindProperty("m_RigLayers");
            layers.ClearArray();
            layers.InsertArrayElementAtIndex(0);

            SerializedProperty layer = layers.GetArrayElementAtIndex(0);
            layer.FindPropertyRelative("m_Rig").objectReferenceValue = rig;
            layer.FindPropertyRelative("m_Active").boolValue = true;
            serializedRigBuilder.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rigBuilder);
        }

        static void ConfigureBinder(GameObject root, Transform avatar, Transform headTarget, Transform leftHandTarget, Transform rightHandTarget)
        {
            global::XRMultiplayer.NetworkHumanoidRigBinder binder = root.GetComponent<global::XRMultiplayer.NetworkHumanoidRigBinder>();
            bool isNewBinder = binder == null;
            if (isNewBinder)
                binder = root.AddComponent<global::XRMultiplayer.NetworkHumanoidRigBinder>();

            SerializedObject serializedBinder = new(binder);
            serializedBinder.FindProperty("m_NetworkPlayer").objectReferenceValue = root.GetComponent<global::XRMultiplayer.XRINetworkPlayer>();
            serializedBinder.FindProperty("m_VisualRoot").objectReferenceValue = avatar;
            serializedBinder.FindProperty("m_HeadTarget").objectReferenceValue = headTarget;
            serializedBinder.FindProperty("m_LeftHandTarget").objectReferenceValue = leftHandTarget;
            serializedBinder.FindProperty("m_RightHandTarget").objectReferenceValue = rightHandTarget;
            if (isNewBinder)
            {
                serializedBinder.FindProperty("m_LeftHandRotationOffsetEuler").vector3Value = new Vector3(0.0f, 0.0f, 90.0f);
                serializedBinder.FindProperty("m_RightHandRotationOffsetEuler").vector3Value = new Vector3(0.0f, 0.0f, -90.0f);
                serializedBinder.FindProperty("m_HeadToRootHeight").floatValue = 1.55f;
                serializedBinder.FindProperty("m_BodyYawSmoothing").floatValue = 12.0f;
            }
            serializedBinder.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(binder);
        }

        static void ConfigureLocomotionDriver(GameObject root, Transform avatar, Animator animator)
        {
            global::XRMultiplayer.AvatarLocomotionAnimatorDriver driver = root.GetComponent<global::XRMultiplayer.AvatarLocomotionAnimatorDriver>();
            if (driver == null)
                driver = root.AddComponent<global::XRMultiplayer.AvatarLocomotionAnimatorDriver>();

            SerializedObject serializedDriver = new(driver);
            serializedDriver.FindProperty("m_Animator").objectReferenceValue = animator;
            serializedDriver.FindProperty("m_MovementRoot").objectReferenceValue = avatar;
            serializedDriver.FindProperty("m_MoveSpeedParameter").stringValue = "MoveSpeed";
            serializedDriver.FindProperty("m_DriveDirectionalParameters").boolValue = true;
            serializedDriver.FindProperty("m_MoveXParameter").stringValue = "MoveX";
            serializedDriver.FindProperty("m_MoveZParameter").stringValue = "MoveZ";
            serializedDriver.FindProperty("m_SpeedForFullWalk").floatValue = 0.01f;
            serializedDriver.FindProperty("m_DeadZone").floatValue = 0.01f;
            serializedDriver.FindProperty("m_DampTime").floatValue = 0.01f;
            serializedDriver.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(driver);
        }

        static void ConfigureLocalOwnerVisibility(GameObject root, Transform avatar)
        {
            global::XRMultiplayer.LocalOwnerSkinnedMeshVisibility visibility = root.GetComponent<global::XRMultiplayer.LocalOwnerSkinnedMeshVisibility>();
            if (visibility == null)
                visibility = root.AddComponent<global::XRMultiplayer.LocalOwnerSkinnedMeshVisibility>();

            SkinnedMeshRenderer[] renderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            SerializedObject serializedVisibility = new(visibility);
            SerializedProperty rendererArray = serializedVisibility.FindProperty("m_Renderers");
            rendererArray.ClearArray();
            for (int i = 0; i < renderers.Length; i++)
            {
                rendererArray.InsertArrayElementAtIndex(i);
                rendererArray.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            }

            serializedVisibility.FindProperty("m_ShowWhenNotSpawned").boolValue = true;
            serializedVisibility.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(visibility);
        }

        static void ConfigurePPEAvatarSetup(GameObject root, Transform avatar, AvatarBoneSet bones)
        {
            Transform ppeRoot = GetOrCreateChild(avatar, "PPE");
            Transform anchorsRoot = GetOrCreateChild(ppeRoot, "Anchors");
            Transform zonesRoot = GetOrCreateChild(ppeRoot, "WearZones");

            Transform coatAnchor = CreateAnchor(anchorsRoot, "CoatAnchor", bones.chest != null ? bones.chest : avatar, Vector3.zero);
            Transform capAnchor = CreateAnchor(anchorsRoot, "CapAnchor", bones.head != null ? bones.head : avatar, new Vector3(0.0f, 0.12f, 0.0f));
            Transform glassesAnchor = CreateAnchor(anchorsRoot, "GlassesAnchor", bones.head != null ? bones.head : avatar, new Vector3(0.0f, 0.0f, 0.12f));
            Transform leftGloveAnchor = CreateAnchor(anchorsRoot, "LeftGloveAnchor", bones.leftHand, Vector3.zero);
            Transform rightGloveAnchor = CreateAnchor(anchorsRoot, "RightGloveAnchor", bones.rightHand, Vector3.zero);

            global::XRMultiplayer.PPE.PPEAvatarEquipmentAnchors anchors = root.GetComponent<global::XRMultiplayer.PPE.PPEAvatarEquipmentAnchors>();
            if (anchors == null)
                anchors = root.AddComponent<global::XRMultiplayer.PPE.PPEAvatarEquipmentAnchors>();

            SerializedObject serializedAnchors = new(anchors);
            serializedAnchors.FindProperty("m_CoatAnchor").objectReferenceValue = coatAnchor;
            serializedAnchors.FindProperty("m_CapAnchor").objectReferenceValue = capAnchor;
            serializedAnchors.FindProperty("m_GlassesAnchor").objectReferenceValue = glassesAnchor;
            serializedAnchors.FindProperty("m_LeftGloveAnchor").objectReferenceValue = leftGloveAnchor;
            serializedAnchors.FindProperty("m_RightGloveAnchor").objectReferenceValue = rightGloveAnchor;
            serializedAnchors.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(anchors);

            ConfigureWearZone(zonesRoot, "CoatWearZone", coatAnchor, global::XRMultiplayer.PPE.PPEItemType.Coat, 0.28f, anchors);
            ConfigureWearZone(zonesRoot, "CapWearZone", capAnchor, global::XRMultiplayer.PPE.PPEItemType.Cap, 0.18f, anchors);
            ConfigureWearZone(zonesRoot, "GlassesWearZone", glassesAnchor, global::XRMultiplayer.PPE.PPEItemType.Glasses, 0.14f, anchors);
            ConfigureWearZone(zonesRoot, "LeftGloveWearZone", leftGloveAnchor, global::XRMultiplayer.PPE.PPEItemType.LeftGlove, 0.12f, anchors);
            ConfigureWearZone(zonesRoot, "RightGloveWearZone", rightGloveAnchor, global::XRMultiplayer.PPE.PPEItemType.RightGlove, 0.12f, anchors);

            global::XRMultiplayer.Assessment.PPETraineeAssessmentState assessmentState = root.GetComponent<global::XRMultiplayer.Assessment.PPETraineeAssessmentState>();
            if (assessmentState == null)
            {
                assessmentState = root.AddComponent<global::XRMultiplayer.Assessment.PPETraineeAssessmentState>();
                EditorUtility.SetDirty(assessmentState);
            }
        }

        static Transform CreateAnchor(Transform anchorsRoot, string anchorName, Transform source, Vector3 localOffset)
        {
            if (source == null)
                return GetOrCreateChild(anchorsRoot, anchorName);

            Transform anchor = GetOrCreateChild(source, anchorName);
            anchor.localPosition = localOffset;
            anchor.localRotation = Quaternion.identity;
            anchor.localScale = Vector3.one;
            return anchor;
        }

        static void ConfigureWearZone(Transform zonesRoot, string zoneName, Transform anchor, global::XRMultiplayer.PPE.PPEItemType itemType, float radius, global::XRMultiplayer.PPE.PPEAvatarEquipmentAnchors anchors)
        {
            Transform zone = GetOrCreateChild(anchor != null ? anchor : zonesRoot, zoneName);
            zone.localPosition = Vector3.zero;
            zone.localRotation = Quaternion.identity;
            zone.localScale = Vector3.one;

            SphereCollider sphereCollider = zone.GetComponent<SphereCollider>();
            if (sphereCollider == null)
                sphereCollider = zone.gameObject.AddComponent<SphereCollider>();

            sphereCollider.isTrigger = true;
            sphereCollider.radius = radius;
            EditorUtility.SetDirty(sphereCollider);

            global::XRMultiplayer.PPE.PPEWearZone wearZone = zone.GetComponent<global::XRMultiplayer.PPE.PPEWearZone>();
            if (wearZone == null)
                wearZone = zone.gameObject.AddComponent<global::XRMultiplayer.PPE.PPEWearZone>();

            SerializedObject serializedWearZone = new(wearZone);
            serializedWearZone.FindProperty("m_AcceptedItemType").enumValueIndex = (int)itemType;
            serializedWearZone.FindProperty("m_AvatarAnchors").objectReferenceValue = anchors;
            serializedWearZone.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(wearZone);
        }

        static void DisableOtherAvatarVisuals(Transform root, Transform activeAvatar)
        {
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                if (animator == null || animator.transform == activeAvatar || animator.transform.IsChildOf(activeAvatar))
                    continue;

                foreach (Renderer renderer in animator.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = false;
                    EditorUtility.SetDirty(renderer);
                }
            }
        }

        static void SetAvatarVisualsEnabled(Transform avatar, bool enabled)
        {
            foreach (Renderer renderer in avatar.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = enabled;
                EditorUtility.SetDirty(renderer);
            }
        }

        static Transform GetOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
                return child;

            GameObject childObject = new(name);
            child = childObject.transform;
            child.SetParent(parent, false);
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }

        static Transform RequireBone(Transform root, string name)
        {
            Transform bone = FindChildRecursive(root, name);
            if (bone == null)
                throw new MissingReferenceException($"Could not find required humanoid bone '{name}'.");

            return bone;
        }

        static Transform FindPreferredAvatar(Transform root, string[] preferredNames)
        {
            foreach (string preferredName in preferredNames)
            {
                Transform avatar = FindChildRecursive(root, preferredName);
                if (avatar != null)
                    return avatar;
            }

            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                if (animator.avatar != null && animator.avatar.isHuman)
                    return animator.transform;
            }

            return null;
        }

        static AvatarBoneSet ResolveBones(Transform avatar)
        {
            Animator animator = avatar.GetComponent<Animator>();
            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            {
                AvatarBoneSet humanoidBones = new()
                {
                    head = animator.GetBoneTransform(HumanBodyBones.Head),
                    chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) != null
                        ? animator.GetBoneTransform(HumanBodyBones.UpperChest)
                        : animator.GetBoneTransform(HumanBodyBones.Chest),
                    leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm),
                    leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm),
                    leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand),
                    rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm),
                    rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm),
                    rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand)
                };

                if (humanoidBones.IsValid)
                    return humanoidBones;
            }

            return new AvatarBoneSet
            {
                head = FindFirstBone(avatar, "mixamorig9:Head", "mixamorig7:Head", "mixamorig:Head", "spine.005"),
                chest = FindFirstBone(avatar, "mixamorig9:Spine2", "mixamorig9:Spine1", "mixamorig7:Spine2", "mixamorig:Spine2", "mixamorig7:Spine1", "mixamorig:Spine1", "spine.003"),
                leftUpperArm = FindFirstBone(avatar, "mixamorig9:LeftArm", "mixamorig7:LeftArm", "mixamorig:LeftArm", "upper_arm.L"),
                leftLowerArm = FindFirstBone(avatar, "mixamorig9:LeftForeArm", "mixamorig7:LeftForeArm", "mixamorig:LeftForeArm", "forearm.L"),
                leftHand = FindFirstBone(avatar, "mixamorig9:LeftHand", "mixamorig7:LeftHand", "mixamorig:LeftHand", "hand.L"),
                rightUpperArm = FindFirstBone(avatar, "mixamorig9:RightArm", "mixamorig7:RightArm", "mixamorig:RightArm", "upper_arm.R"),
                rightLowerArm = FindFirstBone(avatar, "mixamorig9:RightForeArm", "mixamorig7:RightForeArm", "mixamorig:RightForeArm", "forearm.R"),
                rightHand = FindFirstBone(avatar, "mixamorig9:RightHand", "mixamorig7:RightHand", "mixamorig:RightHand", "hand.R")
            }.RequireValid(avatar.name);
        }

        static Transform FindFirstBone(Transform root, params string[] names)
        {
            foreach (string name in names)
            {
                Transform bone = FindChildRecursive(root, name);
                if (bone != null)
                    return bone;
            }

            return null;
        }

        static Transform FindChildRecursive(Transform root, string name)
        {
            if (root.name == name)
                return root;

            foreach (Transform child in root)
            {
                Transform result = FindChildRecursive(child, name);
                if (result != null)
                    return result;
            }

            return null;
        }

        static void CopyPose(Transform target, Transform source)
        {
            target.SetPositionAndRotation(source.position, source.rotation);
        }

        static void PositionElbowHint(Transform hint, Transform upperArm, Transform forearm, float sideOffset)
        {
            hint.position = Vector3.Lerp(upperArm.position, forearm.position, 0.5f)
                + upperArm.root.right * sideOffset
                + Vector3.back * 0.18f;
            hint.rotation = forearm.rotation;
        }

        struct AvatarBoneSet
        {
            public Transform head;
            public Transform chest;
            public Transform leftUpperArm;
            public Transform leftLowerArm;
            public Transform leftHand;
            public Transform rightUpperArm;
            public Transform rightLowerArm;
            public Transform rightHand;

            public bool IsValid =>
                leftUpperArm != null &&
                leftLowerArm != null &&
                leftHand != null &&
                rightUpperArm != null &&
                rightLowerArm != null &&
                rightHand != null;

            public AvatarBoneSet RequireValid(string avatarName)
            {
                if (!IsValid)
                    throw new MissingReferenceException($"Could not resolve required arm bones for avatar '{avatarName}'.");

                return this;
            }
        }
    }
}
