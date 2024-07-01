using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using static STI_Tool.STI;

namespace STI_Tool
{
    public class Settings
    {
        string path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\STI-Tool\\config.xml";

        public string FolderPath {  get; set; }
        public List<string> ImagesPaths { get; set; }
        public string ActualImagePath { get; set; }
        public RECT ROI {  get; set; }
        public string Units { get; set; }
        public double EUFactor { get; set; }
        public double maxDiameter { get; set; }
        public double minDiameter { get; set; }
        public float maxOvality { get; set; }
        public float maxCompacity { get; set; }
        public float maxCompacityHole { get; set; }
        public float alpha { get; set; }
        public int minBlobObjects { get; set; }
        public int validFramesLimit { get; set; }
        public int FH { get; set; }
        public int FFH { get; set; }
        public float align { get; set; }


        public Settings()
        {
            this.FolderPath = "";
            this.ImagesPaths = new List<string>();
            this.ActualImagePath = "";
            this.ROI = new RECT();
            this.Units = "mm";
            this.EUFactor = 1.0;
            this.maxDiameter = 100;
            this.minDiameter = 50;
            this.maxOvality = 0.5f;
            this.maxCompacity = 16;
            this.maxCompacityHole = 18;
            this.alpha = 0.8f;
            this.minBlobObjects = 6;
            this.FH = 5;
            this.FFH = 10;
            this.align = 20;
            this.validFramesLimit = 7;
        }

        public static Settings Load()
        {
            var cls = new Settings();
            string path = cls.path;
            if (File.Exists(path))
            {
                using (var stream = new FileStream(path, FileMode.Open))
                {
                    var serializer = new XmlSerializer(typeof(Settings));
                    return (Settings)serializer.Deserialize(stream);
                }
            }
            else
            {
                // Si el archivo no existe, devolver una instancia con valores predeterminados
                return new Settings();
            }
        }

        public void Save()
        {
            using (var stream = new FileStream(path, FileMode.Create))
            {
                var serializer = new XmlSerializer(typeof(Settings));
                serializer.Serialize(stream, this);
            }
        }

        
    }
}
