namespace SmartFace.Integrations.IFaceManualCall
{
    public class ImageExport
    {
        public ImageExport()
        {
            this.faces = new List<FaceExport>();
        }
        public ICollection<FaceExport> faces { get; set; }
    }

    public class FaceExport
    {
        public string imageName { get; set; }
        public byte[] image { get; internal set; }


        public float detectionQuality { get; internal set; }
        public float faceSize { get; internal set; }

        public float sharpness { get; set; }
        // public float contrast           { get; internal set; }
        public float brightness { get; internal set; }

        // public byte[] crop              { get; internal set; }

        public float pitchAngle { get; internal set; }
        public float yawAngle { get; internal set; }
        public float rollAngle { get; internal set; }

        public float glassStatus { get; internal set; }
        public float glassesWithHeavyFrame { get; internal set; }        
        public float tintedGlasses { get; internal set; }
    }
}