using System;
using OpenCvSharp;
using Innovatrics.IFace;

Console.WriteLine("IFace version {0}", IFace.GetProductString());

IFace.Instance.Init(@"C:\Program Files\Innovatrics\SmartFace\models");

using FaceHandler fh = new FaceHandler();

fh.SetParam(Parameter.FaceDetSpeedAccuracyMode, "balanced_mask");
fh.SetParam(Parameter.FaceDetConfidenceThreshold, "100");

var sourceFolderPath = @"C:\Users\\test-data";

var sourceImages = Directory.GetFiles(sourceFolderPath, "*.jpg");

foreach (var imagePath in sourceImages)
{
    var fileName = Path.GetFileName(imagePath);

    var faces = fh.Detect(IFaceRawImage.loadImage(imagePath), 30, 400, 5);

    Console.WriteLine("{0}. Detected faces: {1} ", fileName, faces.Length);

    foreach (var face in faces)
    {
        Console.WriteLine("{0}. Sharpness: {1} ", fileName, face.GetAttribute(FaceAttributeId.Sharpness));
    }
}