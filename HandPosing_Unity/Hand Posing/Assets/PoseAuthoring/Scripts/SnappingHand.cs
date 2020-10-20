using UnityEngine;
using System.Collections;

using Grabber = Interaction.Grabber;
using Grabbable = Interaction.Grabbable;


namespace PoseAuthoring
{
    public class SnappingHand : MonoBehaviour
    {
        [SerializeField]
        private Grabber grabber;
        [SerializeField]
        private HandPuppet puppet;
        [Space]
        [SerializeField]
        private float snapbackTime = 0.33f;

        private HandGhost _grabbedGhost;
        private HandSnapPose _poseInGhost;

        private float _bonesOverrideFactor;
        private float _offsetOverrideFactor;

        private float _grabStartTime;
        private bool _snapBack;
        private bool _isGrabbing;
        private Pose? _grabOffset;
        private Pose _prevOffset;

        private Coroutine _lastUpdateRoutine;

        private void OnEnable()
        {
            grabber.OnGrabAttemp += GrabAttemp;
            grabber.OnGrabStarted += GrabStarted;
            grabber.OnGrabEnded += GrabEnded;

            puppet.OnPoseUpdated += AttachToObjectOffseted;
            Application.onBeforeRender += VisuallyAttach;

            if(_lastUpdateRoutine == null)
            {
                _lastUpdateRoutine = StartCoroutine(LastUpdate());
            }
        }

        private void OnDisable()
        {
            grabber.OnGrabAttemp -= GrabAttemp;
            grabber.OnGrabStarted -= GrabStarted;
            grabber.OnGrabEnded -= GrabEnded;

            puppet.OnPoseUpdated -= AttachToObjectOffseted;
            Application.onBeforeRender -= VisuallyAttach;

            if (_lastUpdateRoutine != null)
            {
                StopCoroutine(_lastUpdateRoutine);
                _lastUpdateRoutine = null;
            }
        }

        #region grabber callbacks

        private void GrabStarted(Grabbable grabbable)
        {
            var ghostPose = GhostForGrabbable(grabbable);
            if (ghostPose.HasValue)
            {
                _grabbedGhost = ghostPose.Value.Item1;
                _poseInGhost = ghostPose.Value.Item2;
                _offsetOverrideFactor = _bonesOverrideFactor = 1f;

                this.puppet.LerpOffset(_poseInGhost, _grabbedGhost.RelativeTo, _offsetOverrideFactor);
                _grabOffset = new Pose(this.transform.localPosition, this.transform.localRotation);

                _snapBack = grabbable.Snappable.HandSnapBacks;
                _grabStartTime = Time.timeSinceLevelLoad;
                _isGrabbing = true;
            }
        }

        private void GrabEnded(Grabbable grabbable)
        {
            _isGrabbing = false;
            _grabbedGhost = null;
        }

        private void GrabAttemp(Grabbable grabbable, float amount)
        {
            _snapBack = false;
            var ghostPose = GhostForGrabbable(grabbable);
            if (ghostPose.HasValue)
            {
                _grabbedGhost = ghostPose.Value.Item1;
                _poseInGhost = ghostPose.Value.Item2;
                _offsetOverrideFactor = _bonesOverrideFactor = amount;
            }
            else
            {
                _grabbedGhost = null;
                _offsetOverrideFactor = _bonesOverrideFactor = 0f;
            }
        }

        private (HandGhost, HandSnapPose)? GhostForGrabbable(Grabbable grabbable)
        {
            if (grabbable == null)
            {
                return null;
            }
            SnappableObject snappable = grabbable.Snappable;
            if (snappable != null)
            {
                HandSnapPose userPose = this.puppet.TrackedPose(snappable.transform);
                HandGhost ghost = snappable.FindBestGhost(userPose, out float score, out var bestPlace);
                if(ghost != null)
                {
                    HandSnapPose ghostPose = ghost.AdjustPlace(bestPlace);
                    return (ghost, ghostPose);
                }
            }
            return null;
        }

        #endregion

        private YieldInstruction _endOfFrame = new WaitForEndOfFrame();
        private IEnumerator LastUpdate()
        {
            while (true)
            {
                yield return _endOfFrame;
                UndoVisualAttach();
            }
        }

        private void VisuallyAttach()
        {
            if (_isGrabbing
                && _grabbedGhost != null)
            {
                _prevOffset = new Pose(this.transform.localPosition, this.transform.localRotation);
                this.puppet.LerpOffset(_poseInGhost, _grabbedGhost.RelativeTo, 1f);
            }
        }

        private void UndoVisualAttach()
        {
            if (_isGrabbing)
            {
                this.transform.SetPose(_prevOffset, Space.Self);
            }
        }

        private void AttachToObjectOffseted()
        {
            if (_grabbedGhost != null)
            {
                this.puppet.LerpBones(_poseInGhost, _bonesOverrideFactor);
                 
                if (_snapBack)
                {
                    _offsetOverrideFactor = AdjustSnapbackTime(_grabStartTime);
                }
                if(_isGrabbing)
                {
                    if (_snapBack)
                    {
                        this.transform.localRotation = Quaternion.Lerp(this.transform.localRotation, _grabOffset.Value.rotation, _offsetOverrideFactor);
                        this.transform.localPosition = Vector3.Lerp(this.transform.localPosition, _grabOffset.Value.position, _offsetOverrideFactor);
                    }
                    else
                    {
                        this.transform.SetPose(_grabOffset.Value, Space.Self);
                    }
                }
                else
                {
                    this.puppet.LerpOffset(_poseInGhost, _grabbedGhost.RelativeTo, _offsetOverrideFactor);
                }
            }
        }

        private float AdjustSnapbackTime(float grabStartTime)
        {
            return 1f - Mathf.Clamp01((Time.timeSinceLevelLoad - grabStartTime) / snapbackTime);
        }
    }
}
