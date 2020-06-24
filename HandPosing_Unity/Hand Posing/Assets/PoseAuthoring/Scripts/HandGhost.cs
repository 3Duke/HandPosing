﻿using UnityEngine;

namespace PoseAuthoring
{
    [RequireComponent(typeof(HandPuppet))]
    public class HandGhost : MonoBehaviour
    {
        [SerializeField]
        private Renderer handRenderer;
        [SerializeField]
        private Color highlightedColor = Color.yellow;
        [SerializeField]
        private Color defaultColor = Color.blue;
        [SerializeField]
        private string colorProperty = "_RimColor";
        [InspectorButton("MakeStaticPose")]
        public string StaticPose;

        [SerializeField]
        public CylinderSurface _cylinder;
        public CylinderSurface Cylinder
        {
            get
            {
                return _cylinder;
            }
        }

        public HandSnapPose Pose
        {
            get;
            private set;
        }

        public VolumetricPose PoseVolume
        {
            get
            {
                return new VolumetricPose()
                {
                    pose = Pose,
                    volume = Cylinder
                }; 
            }
        }

        public Transform RelativeTo
        {
            get;
            private set;
        }

        private HandPuppet _puppet;
        private HandPuppet Puppet
        {
            get
            {
                if(_puppet == null)
                {
                    _puppet = this.GetComponent<HandPuppet>();
                }
                return _puppet;
            }
        }
        private int colorIndex;

        private void Awake()
        {
            this.colorIndex = Shader.PropertyToID(colorProperty);
            Highlight(false);
        }
        
        public void SetPose(HandSnapPose userPose, Transform relativeTo)
        {
            Puppet.SetRecordedPose(userPose, relativeTo);
            RelativeTo = relativeTo;
            Pose = userPose;
            _cylinder = new CylinderSurface(Puppet.Grip);
            _cylinder.MakeSinglePoint();
        }

        public void SetPoseVolume(VolumetricPose poseVolume, Transform relativeTo)
        {
            SetPose(poseVolume.pose, relativeTo);
            _cylinder = poseVolume.volume;
            _cylinder.Grip = Puppet.Grip;
        }

        public void Highlight(float amount)
        {
            Color color = Color.Lerp(defaultColor, highlightedColor, amount);
            handRenderer.material.SetColor(colorIndex, color);
        }

        public void Highlight(bool highlight)
        {
            Color color = highlight ? highlightedColor : defaultColor;
            handRenderer.material.SetColor(colorIndex, color);
        }

        public void MakeStaticPose()
        {
            _cylinder.MakeSinglePoint();
        }

        private void Reset()
        {
            _cylinder = new CylinderSurface(Puppet.Grip);
            handRenderer = this.GetComponentInChildren<SkinnedMeshRenderer>();
        }

        public float Score(HandSnapPose userPose, float maxDistance = 0.1f)
        {
            HandSnapPose snapPose = this.Pose;
            if (snapPose.isRightHand != userPose.isRightHand)
            {
                return 0f;
            }

            Vector3 globalPosDesired = RelativeTo.TransformPoint(userPose.relativeGripPos);
            Vector3 surfacePoint = Cylinder.NearestPointInSurface(globalPosDesired);

            Quaternion globalRotDesired = RelativeTo.rotation * userPose.relativeGripRot;
            Quaternion surfaceRotation = Cylinder.TransformRotation(userPose, RelativeTo, surfacePoint);

            float forwardDifference = Vector3.Dot(surfaceRotation * Vector3.forward, globalRotDesired * Vector3.forward) * 0.5f + 0.5f;
            float upDifference = Vector3.Dot(surfaceRotation * Vector3.up, globalRotDesired * Vector3.up) * 0.5f + 0.5f;

            float positionDifference = 1f - Mathf.Clamp01(Vector3.Distance(surfacePoint, globalPosDesired) / maxDistance);

            return forwardDifference * upDifference * positionDifference;
        }

        public HandSnapPose AdjustPoseToVolume(HandSnapPose userPose)
        {
            HandSnapPose snapPose = this.Pose;

            Vector3 globalPosDesired = RelativeTo.TransformPoint(userPose.relativeGripPos);
            Vector3 surfacePoint = Cylinder.NearestPointInSurface(globalPosDesired);
            Quaternion surfaceRotation = Cylinder.TransformRotation(userPose, RelativeTo, surfacePoint);

            snapPose.relativeGripPos = RelativeTo.InverseTransformPoint(surfacePoint);
            snapPose.relativeGripRot = Quaternion.Inverse(RelativeTo.rotation) * surfaceRotation;

            return snapPose;
        }
    }
}
