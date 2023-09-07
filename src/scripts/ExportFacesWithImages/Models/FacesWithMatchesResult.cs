namespace Innovatrics.SmartFace.Integrations.ExportFacesWithImages.Models
{
    internal class FacesWithMatchesResult
    {
        public FacesWithMatchesResultFaces faces { get; set; }
    }

    internal class FacesWithMatchesResultFaces
    {
        public Face[] items { get; set; }
    }
}