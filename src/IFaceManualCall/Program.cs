using System;
using System.Text;
using OpenCvSharp;
using Innovatrics.IFace;

namespace SmartFace.Integrations.IFaceManualCall
{
    public class Program
    {
        private static void Main()
        {
            Console.WriteLine("IFace version {0}", IFace.GetProductString());

            IFace.Instance.Init(@"C:\Program Files\Innovatrics\SmartFace\models");

            using FaceHandler fh = new FaceHandler();

            fh.SetParam(Parameter.FaceDetSpeedAccuracyMode, "balanced_mask");
            fh.SetParam(Parameter.FaceDetConfidenceThreshold, "100");

            var sourceFolderPath = @"C:\Users\user\Downloads\test-data";

            var sourceImages = Directory.GetFiles(sourceFolderPath, "*.jpg");

            var results = new List<ImageExport>();

            foreach (var imagePath in sourceImages)
            {
                var fileName = Path.GetFileName(imagePath);

                var faces = fh.Detect(IFaceRawImage.loadImage(imagePath), 30, 400, 5);

                Console.WriteLine("{0}. Detected faces: {1} ", fileName, faces.Length);

                var result = new ImageExport
                {
                    faces = new List<FaceExport>()
                };

                results.Add(result);

                foreach (var face in faces)
                {
                    result.faces.Add(new FaceExport
                    {
                        imageName = Path.GetFileName(imagePath),
                        image = File.ReadAllBytes(imagePath),

                        detectionQuality = face.GetAttribute(FaceAttributeId.FaceConfidence),
                        faceSize = face.GetAttribute(FaceAttributeId.FaceSize),

                        sharpness = face.GetAttribute(FaceAttributeId.Sharpness),
                        brightness = face.GetAttribute(FaceAttributeId.Brightness),

                        pitchAngle = face.GetAttribute(FaceAttributeId.PitchAngle),
                        yawAngle = face.GetAttribute(FaceAttributeId.YawAngle),
                        rollAngle = face.GetAttribute(FaceAttributeId.RollAngle),

                        glassesWithHeavyFrame = face.GetAttribute(FaceAttributeId.HeavyFrame),
                        // tintedGlasses = face.GetAttribute(FaceAttributeId.TintedGlasses),
                        // glassStatus = face.GetAttribute(FaceAttributeId.GlassStatus)
                    });
                }
            }

            var f = results.SelectMany(s => s.faces).ToArray();

            HtmlExporter.ExportResultsToHtml(@"C:\Users\user\Downloads\test-results.html", f);
        }

        private static byte[] EncodeRawToDefaultImageFormat(IFaceRawImage cropImage)
        {
            using var mat = new Mat(cropImage.Height, cropImage.Width, MatType.CV_8UC3, cropImage.Data);
            return mat.ToBytes(".jpg", new ImageEncodingParam(ImwriteFlags.JpegQuality, 90));
        }
    }
}