using UnityEngine;

namespace AirSticker.Runtime.Scripts.Core
{
    /// <summary>
    ///     Line of convex polygons.
    /// </summary>
    public struct Line
    {
        public Vector3 StartPosition { get; private set; }
        public Vector3 EndPosition { get; private set; }
        public Vector3 StartToEndVec { get; private set; }
        public Vector3 StartNormal { get; private set; }
        public Vector3 EndNormal { get; private set; }
        public BoneWeight StartWeight { get; private set; }
        public BoneWeight EndWeight { get; private set; }
        public Vector3 StartLocalPosition { get; private set; }
        public Vector3 EndLocalPosition { get; private set; }
        public Vector3 StartLocalNormal { get; private set; }
        public Vector3 EndLocalNormal { get; private set; }

        /// <summary>
        ///     Initialize.
        /// </summary>
        public void Initialize(Vector3 startPositionInWorldSpace, Vector3 endPositionInWorldSpace, 
            Vector3 startNormalInWorldSpace, Vector3 endNormalInWorldSpace,
            BoneWeight startWeight, BoneWeight endWeight, Vector3 startPositionInLocalSpace, 
            Vector3 endPositionInLocalSpace, Vector3 startNormalInLocalSpace, Vector3 endNormalInLocalSpace)
        {
            StartPosition = startPositionInWorldSpace;
            EndPosition = endPositionInWorldSpace;
            StartNormal = startNormalInWorldSpace;
            EndNormal = endNormalInWorldSpace;
            StartToEndVec = EndPosition - StartPosition;
            StartWeight = startWeight;
            EndWeight = endWeight;
            StartLocalPosition = startPositionInLocalSpace;
            EndLocalPosition = endPositionInLocalSpace;
            StartLocalNormal = startNormalInLocalSpace;
            EndLocalNormal = endNormalInLocalSpace;
        }

        /// <summary>
        ///     After setting the end position of the line, calculate the from start to end vector.
        /// </summary>
        public void SetEndAndCalcStartToEnd(Vector3 newEndPositionInWorldSpace, Vector3 newEndNormalInWorldSpace,
            Vector3 newEndPositionInLocalSpace, Vector3 newEndNormalInLocalSpace)
        {
            EndPosition = newEndPositionInWorldSpace;
            EndNormal = newEndNormalInWorldSpace;
            EndLocalPosition = newEndPositionInLocalSpace;
            EndLocalNormal = newEndNormalInLocalSpace;
            StartToEndVec = EndPosition - StartPosition;
        }

        /// <summary>
        ///     After setting the start and end position of the line, calculate the from start to end vector.
        /// </summary>
        public void SetStartEndAndCalcStartToEnd(
            Vector3 newStartPositionInWorldSpace,
            Vector3 newEndPositionInWorldSpace,
            Vector3 newStartNormalInWorldSpace,
            Vector3 newEndNormalInWorldSpace,
            Vector3 newStartPositionInLocalSpace,
            Vector3 newEndPositionInLocalSpace,
            Vector3 newStartNormalInLocalSpace,
            Vector3 newEndNormalInLocalSpace)
        {
            StartPosition = newStartPositionInWorldSpace;
            EndPosition = newEndPositionInWorldSpace;
            StartNormal = newStartNormalInWorldSpace;
            EndNormal = newEndNormalInWorldSpace;

            StartLocalPosition = newStartPositionInLocalSpace;
            EndLocalPosition = newEndPositionInLocalSpace;
            StartLocalNormal = newStartNormalInLocalSpace;
            EndLocalNormal = newEndNormalInLocalSpace;

            StartToEndVec = EndPosition - StartPosition;
        }
        
        public void SetStartEndBoneWeights(BoneWeight newStartWeight, BoneWeight newEndWeight)
        {
            StartWeight = newStartWeight;
            EndWeight = newEndWeight;
        }
        
        public void SetEndBoneWeight(BoneWeight newEndWeight)
        {
            EndWeight = newEndWeight;
        }
    }
}
