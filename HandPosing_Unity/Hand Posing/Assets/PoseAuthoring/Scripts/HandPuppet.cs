﻿using OVRTouchSample;
using System.Collections.Generic;
using UnityEngine;
using static OVRSkeleton;

namespace PoseAuthoring
{
    public class HandPuppet : MonoBehaviour
    {
        [SerializeField]
        private OVRSkeleton trackedHand;
        [SerializeField]
        private Transform handAnchor;
        [SerializeField]
        private Transform gripPoint;
        [SerializeField]
        private bool isRightHand;
        [SerializeField]
        private List<BoneMap> boneMaps;

        private Dictionary<BoneId, BoneMap> bonesCollection;
        private Dictionary<BoneId, (Vector3, Quaternion)> originalBonePosisitions;

        private (Vector3, Quaternion) _originalGripPose;
        private (Vector3, Quaternion) _pupettedGripPose;
        private (Vector3, Quaternion) GripPose
        {
            get
            {
                return _puppettedHand ? _pupettedGripPose : _originalGripPose;
            }
        }

        private bool _initialized;
        private bool _restored;
        private bool _puppettedHand;

        [SerializeField]
        private Transform axis;

        private void Awake()
        {
            InitializeBones();
            StoreOriginalBonePositions();
            InitializeGrip();
            if (trackedHand == null)
            {
                this.enabled = false;
            }
        }

        private void InitializeBones()
        {
            if (_initialized)
            {
                return;
            }
            bonesCollection = new Dictionary<BoneId, BoneMap>();
            foreach (var boneMap in boneMaps)
            {
                BoneId id = boneMap.id;
                bonesCollection.Add(id, boneMap);
            }
            _initialized = true;
        }

        private void InitializeGrip()
        {
            Vector3 relativePosition = this.handAnchor.InverseTransformPoint(this.gripPoint.position);
            Quaternion relativeRotation = this.handAnchor.rotation * this.gripPoint.rotation;
            _originalGripPose = (relativePosition, relativeRotation);

            SetEmptyPose();

            relativePosition = this.handAnchor.InverseTransformPoint(this.gripPoint.position);
            relativeRotation = this.handAnchor.rotation * this.gripPoint.rotation;
            _pupettedGripPose = (relativePosition, relativeRotation);

            SetOriginalBonePositions();

        }
        private void Update()
        {
            Vector3 trackedGripPosition = this.handAnchor.TransformPoint(GripPose.Item1);
            Quaternion trackedGripRotation = this.handAnchor.rotation * GripPose.Item2;
            axis.position = trackedGripPosition;
            axis.rotation = trackedGripRotation;
        }

        private void LateUpdate()
        {
            if (trackedHand != null
                && trackedHand.IsInitialized
                && trackedHand.IsDataValid)
            {
                _restored = false;
                _puppettedHand = true;
                SetLivePose(trackedHand);
            }
            else if (!_restored)
            {
                _restored = true;
                _puppettedHand = false;
                SetOriginalBonePositions();
            }
        }

        #region bone restoring
        private void StoreOriginalBonePositions()
        {
            Dictionary<BoneId, (Vector3, Quaternion)> bonePositions = new Dictionary<BoneId, (Vector3, Quaternion)>();
            foreach (var boneMap in boneMaps)
            {
                Vector3 localPosition = boneMap.transform.localPosition;
                Quaternion localRotation = boneMap.transform.localRotation;
                bonePositions.Add(boneMap.id, (localPosition, localRotation));
            }
            originalBonePosisitions = bonePositions;
        }

        private void SetOriginalBonePositions()
        {
            foreach (var bonePosition in originalBonePosisitions)
            {
                Transform bone = bonesCollection[bonePosition.Key].transform;
                bone.localPosition = bonePosition.Value.Item1;
                bone.localRotation = bonePosition.Value.Item2;
            }
        }
        #endregion

        private void SetLivePose(OVRSkeleton skeleton)
        {
            for (int i = 0; i < skeleton.Bones.Count; ++i)
            {
                BoneId boneId = (BoneId)skeleton.Bones[i].Id;
                if (bonesCollection.ContainsKey(boneId))
                {
                    Transform boneTransform = bonesCollection[boneId].transform;
                    Quaternion offset = Quaternion.Euler(bonesCollection[boneId].rotationOffset);
                    Quaternion desiredRot = skeleton.Bones[i].Transform.localRotation;
                    boneTransform.localRotation = offset * desiredRot;

                    if (bonesCollection[boneId].updatePosition)
                    {
                        boneTransform.localPosition = bonesCollection[boneId].positionOffset + skeleton.Bones[i].Transform.localPosition;
                    }
                }
            }
        }

        private void SetEmptyPose()
        {
            foreach (var bone in boneMaps)
            {
                Transform boneTransform = bone.transform;
                Quaternion offset = Quaternion.Euler(bone.rotationOffset) ;
                boneTransform.localRotation = offset;

                if (bone.updatePosition)
                {
                    boneTransform.localPosition = bone.positionOffset;
                }
            }

        }

        public void SetRecordedPose(HandPose pose, Transform relativeTo, float weight = 1f)
        {
            InitializeBones();
            foreach (var bone in pose.Bones)
            {
                BoneId boneId = bone.boneID;
                if (bonesCollection.ContainsKey(boneId))
                {
                    Transform boneTransform = bonesCollection[boneId].transform;
                    boneTransform.localRotation = bone.rotation;
                }
            }

            Quaternion rotationDif = Quaternion.Inverse(this.gripPoint.rotation) * this.transform.rotation;
            this.transform.rotation = Quaternion.Lerp(this.transform.rotation,
                rotationDif * (pose.handGripRotation * relativeTo.rotation),
                weight);

            Vector3 positionDif = this.transform.position - this.gripPoint.position;
            this.transform.position = Vector3.Lerp(this.transform.position,
                relativeTo.TransformPoint(pose.handGripPosition) + positionDif,
                weight);
        }

        public HandPose CurrentPoseVisual(Transform relativeTo)
        {
            HandPose pose = new HandPose();
            pose.handGripPosition = relativeTo != null ? relativeTo.InverseTransformPoint(this.gripPoint.position) : this.gripPoint.position;
            pose.handGripRotation = relativeTo != null ? relativeTo.rotation * this.gripPoint.rotation : this.gripPoint.rotation;
            pose.isRightHand = isRightHand;

            foreach (var bone in bonesCollection)
            {
                BoneMap boneMap = bone.Value;
                Quaternion rotation = boneMap.transform.localRotation;
                pose.Bones.Add(new BoneRotation() { boneID = boneMap.id, rotation = rotation });
            }
            return pose;
        }

        public HandPose CurrentPoseTracked(Transform relativeTo)
        {
            Vector3 trackedGripPosition = handAnchor.TransformPoint(GripPose.Item1);
            Quaternion trackedGripRotation = GripPose.Item2 * Quaternion.Inverse(handAnchor.rotation);

            HandPose pose = new HandPose();
            pose.handGripPosition = relativeTo != null ? relativeTo.InverseTransformPoint(trackedGripPosition) : trackedGripPosition;
            pose.handGripRotation = relativeTo != null ? relativeTo.rotation * trackedGripRotation : trackedGripRotation;
            pose.isRightHand = isRightHand;
            return pose;
        }
    }

}