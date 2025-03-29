using System;

namespace Innovatrics.SmartFace.Integrations.AeosSync.Clients
{
    public class FacesResponse
    {
        public Faces Faces { get; set; }
    }

    public class Faces
    {
        public Face[] Items { get; set; }
    }

    public class Face
    {
        public Guid Id { get; set; }
        public string ImageDataId { get; set; }
        public FaceType FaceType { get; set; }
        public string CreatedAt { get; set; }
    }
}