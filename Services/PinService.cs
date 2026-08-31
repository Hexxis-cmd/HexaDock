using System.Security.Cryptography;
using System.Text;
using HexaDock.Models;

namespace HexaDock.Services;

public static class PinService
{
    private const int Iterations = 210_000;
    private const int HashLength = 32;

    public static PinSettings Create(string pin, string question, string answer)
    {
        var pinSalt = RandomNumberGenerator.GetBytes(16);
        var answerSalt = RandomNumberGenerator.GetBytes(16);
        return new PinSettings
        {
            PinSalt = Convert.ToBase64String(pinSalt),
            PinHash = Convert.ToBase64String(Hash(pin.Trim(), pinSalt)),
            Question = question.Trim(),
            AnswerSalt = Convert.ToBase64String(answerSalt),
            AnswerHash = Convert.ToBase64String(Hash(NormalizeAnswer(answer), answerSalt))
        };
    }

    public static bool VerifyPin(PinSettings settings, string pin) => Verify(pin.Trim(), settings.PinSalt, settings.PinHash);
    public static bool VerifyAnswer(PinSettings settings, string answer) => Verify(NormalizeAnswer(answer), settings.AnswerSalt, settings.AnswerHash);

    private static bool Verify(string value, string saltText, string hashText)
    {
        try
        {
            var salt = Convert.FromBase64String(saltText);
            var expected = Convert.FromBase64String(hashText);
            return CryptographicOperations.FixedTimeEquals(Hash(value, salt), expected);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] Hash(string value, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(value), salt, Iterations, HashAlgorithmName.SHA256, HashLength);

    private static string NormalizeAnswer(string answer) => answer.Trim().ToUpperInvariant();
}
