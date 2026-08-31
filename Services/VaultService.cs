using System.IO;
using System.Security.Cryptography;
using HexaDock.Models;

namespace HexaDock.Services;

public static class VaultService
{
    private const int ChunkSize = 1024 * 1024;
    private static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HexaDock", "Vault");
    private static string KeyPath => Path.Combine(Root, "vault.key");

    public static VaultItem Import(string source)
    {
        Directory.CreateDirectory(Root);
        var item = new VaultItem { Id = Guid.NewGuid().ToString("N"), Name = Path.GetFileName(source), Size = new FileInfo(source).Length, Added = DateTime.Now };
        var finalPath = ItemPath(item.Id);
        var temporary = finalPath + ".tmp";
        Encrypt(source, temporary, GetKey());
        File.Move(temporary, finalPath, true);
        return item;
    }

    public static void Export(VaultItem item, string destination)
    {
        var temporary = destination + $".{Guid.NewGuid():N}.tmp";
        Decrypt(ItemPath(item.Id), temporary, GetKey());
        File.Move(temporary, destination, true);
    }

    public static void Delete(VaultItem item)
    {
        var path = ItemPath(item.Id);
        if (File.Exists(path)) File.Delete(path);
    }

    public static string ItemPath(string id) => Path.Combine(Root, id + ".hxd");

    public static void RunSelfTest()
    {
        var root = Path.Combine(Path.GetTempPath(), "HexaDockSelfTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source.bin");
            var encrypted = Path.Combine(root, "encrypted.hxd");
            var restored = Path.Combine(root, "restored.bin");
            var content = RandomNumberGenerator.GetBytes(ChunkSize + 137);
            File.WriteAllBytes(source, content);
            var key = RandomNumberGenerator.GetBytes(32);
            Encrypt(source, encrypted, key);
            Decrypt(encrypted, restored, key);
            if (!CryptographicOperations.FixedTimeEquals(content, File.ReadAllBytes(restored)))
                throw new InvalidOperationException("Vault encryption self-test failed.");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static byte[] GetKey()
    {
        if (File.Exists(KeyPath)) return ProtectedData.Unprotect(File.ReadAllBytes(KeyPath), null, DataProtectionScope.CurrentUser);
        var key = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(KeyPath, ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser));
        return key;
    }

    private static void Encrypt(string source, string destination, byte[] key)
    {
        using var input = File.OpenRead(source);
        using var output = File.Create(destination);
        using var writer = new BinaryWriter(output);
        using var aes = new AesGcm(key, 16);
        writer.Write(new byte[] { (byte)'H', (byte)'X', (byte)'D', (byte)'1' });
        var buffer = new byte[ChunkSize];
        int count;
        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            var nonce = RandomNumberGenerator.GetBytes(12);
            var tag = new byte[16];
            var cipher = new byte[count];
            aes.Encrypt(nonce, buffer.AsSpan(0, count), cipher, tag);
            writer.Write(count);
            writer.Write(nonce);
            writer.Write(tag);
            writer.Write(cipher);
        }
        writer.Write(0);
    }

    private static void Decrypt(string source, string destination, byte[] key)
    {
        using var input = File.OpenRead(source);
        using var reader = new BinaryReader(input);
        using var output = File.Create(destination);
        using var aes = new AesGcm(key, 16);
        if (!reader.ReadBytes(4).SequenceEqual(new byte[] { (byte)'H', (byte)'X', (byte)'D', (byte)'1' }))
            throw new InvalidDataException("Not a HexaDock vault file.");
        while (true)
        {
            var count = reader.ReadInt32();
            if (count == 0) break;
            if (count < 0 || count > ChunkSize) throw new InvalidDataException("Invalid vault chunk.");
            var nonce = reader.ReadBytes(12);
            var tag = reader.ReadBytes(16);
            var cipher = reader.ReadBytes(count);
            if (nonce.Length != 12 || tag.Length != 16 || cipher.Length != count) throw new EndOfStreamException();
            var plain = new byte[count];
            aes.Decrypt(nonce, cipher, tag, plain);
            output.Write(plain);
        }
    }
}
