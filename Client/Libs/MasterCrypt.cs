using System;
using System.Security.Cryptography;

public static class MasterCrypt {
    private static int IDynamicChange = 74;
    private static string SDynamicChange = "6akRFgkB4xCPzj76NLtSUWaaVpcx65Q3";
    private static byte RandomForce = 100;

    //Crypting methods
    private static byte[] EncryptBytes(byte[] plainBytes, byte[] key) {
        //Define
        int size = plainBytes.Length;
        if (size == 0)
            return new byte[0];
        byte[] buildInKey = CreateMasterKey(Converter.GetBytes(SDynamicChange));
        byte[] encryptedBytes = new byte[size + 1];
        encryptedBytes[0] = buildInKey[0];
        byte masterRandom = (byte) new Random().Next(2, RandomForce - 1);
        byte[] masterKey = null;
        if (key != null)
            masterKey = CreateMasterKey(key);

        for (int i = 0; i < encryptedBytes.Length; i++) {
            if (i > 0) {
                encryptedBytes[i] = (byte) EncryptionAlgoritm(plainBytes[size - i], i, size);
                encryptedBytes[i] += buildInKey[i % buildInKey.Length];
                encryptedBytes[i] += encryptedBytes[i - 1];
                if (masterKey != null)
                    encryptedBytes[i] += masterKey[i % masterKey.Length];
            }
            encryptedBytes[i] += masterRandom;
        }

        return encryptedBytes;
    }

    private static byte[] DecryptBytes(byte[] encryptedBytes, byte[] key) {
        //Define
        int size = encryptedBytes.Length;
        if (size == 0)
            return new byte[0];
        byte[] buildInKey = CreateMasterKey(Converter.GetBytes(SDynamicChange));
        byte[] plainBytes = new byte[size - 1];
        byte masterRandom = (byte) (encryptedBytes[0] - buildInKey[0]);
        byte[] masterKey = null;
        if (key != null)
            masterKey = CreateMasterKey(key);

        for (int i = 0; i < plainBytes.Length; i++) {
            plainBytes[i] = encryptedBytes[size - i - 1];
            plainBytes[i] -= masterRandom;

            if (masterKey != null)
                plainBytes[i] -= masterKey[(size - i - 1) % masterKey.Length];

            plainBytes[i] -= encryptedBytes[size - i - 2];
            plainBytes[i] -= buildInKey[(size - i - 1) % buildInKey.Length];
            plainBytes[i] = (byte) DecryptionAlgoritm(plainBytes[i], size - i - 1, size - 1);
        }

        return plainBytes;
    }

    //Public usage
    #region Overloads
    //Encrypt
    //With bytes
    public static byte[] Encrypt(byte[] plainBytes, byte[] key) {
        return EncryptBytes(plainBytes, key);
    }

    public static byte[] Encrypt(byte[] plainBytes, string key) {
        return EncryptBytes(plainBytes, Converter.GetBytes(key));
    }

    public static byte[] Encrypt(byte[] plainBytes) {
        return EncryptBytes(plainBytes, null);
    }

    //With string
    public static string Encrypt(string plainBytes, byte[] key, bool isNumerical = false) {
        if (isNumerical)
            return Converter.GetNumeric(Converter.GetString(EncryptBytes(Converter.GetBytes(plainBytes), key)));
        else
            return Converter.GetString(EncryptBytes(Converter.GetBytes(plainBytes), key));
    }

    public static string Encrypt(string plainBytes, string key, bool isNumerical = false) {
        if (isNumerical)
            return Converter.GetNumeric(Converter.GetString(EncryptBytes(Converter.GetBytes(plainBytes), Converter.GetBytes(key))));
        else
            return Converter.GetString(EncryptBytes(Converter.GetBytes(plainBytes), Converter.GetBytes(key)));
    }

    public static string Encrypt(string plainBytes, bool isNumerical = false) {
        if (isNumerical)
            return Converter.GetNumeric(Converter.GetString(EncryptBytes(Converter.GetBytes(plainBytes), null)));
        else
            return Converter.GetString(EncryptBytes(Converter.GetBytes(plainBytes), null));
    }

    //Decrypt
    //With bytes
    public static byte[] Decrypt(byte[] encryptedBytes, byte[] key) {
        return DecryptBytes(encryptedBytes, key);
    }

    public static byte[] Decrypt(byte[] encryptedBytes, string key) {
        return DecryptBytes(encryptedBytes, Converter.GetBytes(key));
    }

    public static byte[] Decrypt(byte[] encryptedBytes) {
        return DecryptBytes(encryptedBytes, null);
    }

    //With string
    public static string Decrypt(string encryptedText, byte[] key) {
        if (IsNumerical(encryptedText))
            encryptedText = Converter.GetString(encryptedText);
        return Converter.GetString(DecryptBytes(Converter.GetBytes(encryptedText), key));
    }

    public static string Decrypt(string encryptedText, string key) {
        if (IsNumerical(encryptedText))
            encryptedText = Converter.GetString(encryptedText);
        return Converter.GetString(DecryptBytes(Converter.GetBytes(encryptedText), Converter.GetBytes(key)));
    }

    public static string Decrypt(string encryptedText) {
        if (IsNumerical(encryptedText))
            encryptedText = Converter.GetString(encryptedText);
        return Converter.GetString(DecryptBytes(Converter.GetBytes(encryptedText), null));
    }
    #endregion

    //Private stuff
    public static class Converter {
        public static byte[] GetBytes(string text) {
            if (text == null || text.Length == 0)
                return null;
            byte[] bytes = new byte[text.Length * 2];
            for (int i = 0; i < bytes.Length; i += 2) {
                byte[] charBytes = BitConverter.GetBytes(text[i / 2]);
                bytes[i] = charBytes[0];
                bytes[i + 1] = charBytes[1];
            }

            if (bytes[bytes.Length - 1] == 0) {
                byte[] temp = new byte[bytes.Length - 1];
                Array.Copy(bytes, temp, bytes.Length - 1);
                bytes = temp;
            }

            return bytes;
        }

        public static string GetString(byte[] bytes) {
            if (bytes == null)
                return null;
            if (bytes.Length % 2 != 0) {
                byte[] temp = new byte[bytes.Length + 1];
                Array.Copy(bytes, temp, bytes.Length);
                bytes = temp;
            }
            char[] text = new char[bytes.Length / 2];
            for (int i = 0; i < bytes.Length; i += 2) {
                text[i / 2] = BitConverter.ToChar(bytes, i);
            }

            return new string(text);
        }

        public static string GetString(string numericalText) {
            string[] nums = numericalText.Split('.');
            string text = "";
            for (int i = 0; i < nums.Length; i++) {
                text += (char) Convert.ToInt32(nums[i], 16);
            }
            return text;
        }

        public static string GetNumeric(string text) {
            string numText = "";
            for (int i = 0; i < text.Length; i++) {
                numText += ((int) text[i]).ToString("X");
                if ((i + 1) < text.Length)
                    numText += '.';
            }
            return numText;
        }
    }

    private static int EncryptionAlgoritm(int plainInt, int offset = 0, int lenght = 1) {
        int result = plainInt;
        result += IDynamicChange;
        result += lenght + (lenght % IDynamicChange);
        result += -offset - (offset % IDynamicChange);
        result += (int) Math.Exp(IDynamicChange * (Math.Abs(offset) + 1) * (Math.Abs(lenght) + 1)) % 255;
        result = IDynamicChange % lenght ^ result;
        result *= (int) Math.Pow(-1, offset);
        return result;
    }

    private static int DecryptionAlgoritm(int encryptedInt, int offset = 0, int lenght = 1) {
        int result = encryptedInt;
        result *= (int) Math.Pow(-1, offset);
        result = result ^ IDynamicChange % lenght;
        result -= (int) Math.Exp(IDynamicChange * (Math.Abs(offset) + 1) * (Math.Abs(lenght) + 1)) % 255;
        result -= -offset - (offset % IDynamicChange);
        result -= lenght + (lenght % IDynamicChange);
        result -= IDynamicChange;
        return result;
    }

    private static byte[] CreateMasterKey(byte[] key) {
        byte[] keyHash = SHA256.Create().ComputeHash(key);
        byte[] masterKey = new byte[keyHash.Length];

        //Calculating geomitric middle of key
        int geometricMiddle = key[0];
        for (int i = 1; i < key.Length; i++) {
            geometricMiddle *= key[i];
        }
        geometricMiddle = (int) Math.Pow(geometricMiddle, 1 / key.Length);

        for (int i = 0; i < keyHash.Length; i++) {
            masterKey[i] = (byte) (keyHash[i] + key[i % key.Length] + geometricMiddle);
        }

        return masterKey;
    }

    private static bool IsNumerical(string text) {
        string[] nums = text.Split('.');
        for (int i = 0; i < nums.Length; i++) {
            int outer;
            if (!int.TryParse(nums[i], System.Globalization.NumberStyles.HexNumber, null, out outer))
                return false;
        }
        return true;
    }
}