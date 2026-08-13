using System.IO;
using UnityEngine;

/*
 * TransparentPNGConverter.cs
 * Thư mục: Assets/ThanhVien/NhatTien/Script/UI/Editor/
 */
public static class TransparentPNGConverter
{
    public static void MakeWhiteTransparent(string inputJpgPath, string outputPngPath)
    {
        byte[] fileData = File.ReadAllBytes(inputJpgPath);
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(fileData);

        Color32[] pixels = tex.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].r > 240 && pixels[i].g > 240 && pixels[i].b > 240)
            {
                pixels[i] = new Color32(0, 0, 0, 0); // Đổi màu trắng thành trong suốt hoàn toàn
            }
        }

        Texture2D newTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
        newTex.SetPixels32(pixels);
        newTex.Apply();

        byte[] pngBytes = newTex.EncodeToPNG();
        File.WriteAllBytes(outputPngPath, pngBytes);
        Debug.Log("✅ Đã tạo file PNG tách phông sạch hoàn toàn: " + outputPngPath);
    }
}
