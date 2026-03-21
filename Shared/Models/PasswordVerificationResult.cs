namespace AuthService.Shared.Models;

public sealed record PasswordVerificationResult(bool Succeeded, bool NeedsRehash)
{
    public static PasswordVerificationResult Success(bool needsRehash = false) => new(true, needsRehash);
    public static PasswordVerificationResult Failure() => new(false, false);
}
