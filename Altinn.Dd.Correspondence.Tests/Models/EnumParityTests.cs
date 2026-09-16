using Altinn.Dd.Correspondence.HttpClients;

namespace Altinn.Dd.Correspondence.Tests;

/// <summary>
/// The library keeps its own public copies of several enums so the generated client stays internal,
/// then converts between them with unchecked casts - see Features.Search.Handler,
/// Features.Send.Handler and Extensions.Mapper. An unchecked cast between two independently
/// declared enums cannot fail at build time or at run time: if a regenerated client renumbered or
/// reordered a member, every conversion would silently produce the wrong value.
///
/// These tests are what makes that safe. Each one pins a public enum against the generated enum it
/// is cast to, member for member, by name and by numeric value.
/// </summary>
public class EnumParityTests
{
    private static void AssertSameShape<TPublic, TGenerated>()
        where TPublic : struct, Enum
        where TGenerated : struct, Enum
    {
        static Dictionary<string, long> Members<T>() where T : struct, Enum =>
            Enum.GetValues<T>().ToDictionary(
                value => Enum.GetName(value)!,
                value => Convert.ToInt64(value));

        var expected = Members<TGenerated>();
        var actual = Members<TPublic>();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EmailContentType_MatchesTheGeneratedEnum() =>
        AssertSameShape<Models.EmailContentType, EmailContentType>();

    [Fact]
    public void CorrespondenceStatus_MatchesTheGeneratedEnum() =>
        AssertSameShape<Models.CorrespondenceStatus, CorrespondenceStatusExt>();

    [Fact]
    public void InitializedNotificationStatus_MatchesTheGeneratedEnum() =>
        AssertSameShape<Models.InitializedNotificationStatus, InitializedNotificationStatusExt>();

    [Fact]
    public void GetCorrespondenceStatus_MatchesTheGeneratedEnum() =>
        AssertSameShape<Features.Get.CorrespondenceStatus, CorrespondenceStatusExt>();

    [Fact]
    public void GetEmailContentType_MatchesTheGeneratedEnum() =>
        AssertSameShape<Features.Get.EmailContentType, EmailContentType>();

    [Fact]
    public void GetNotificationChannel_MatchesTheGeneratedEnum() =>
        AssertSameShape<Features.Get.NotificationChannel, NotificationChannelExt>();

    [Fact]
    public void GetNotificationTemplate_MatchesTheGeneratedEnum() =>
        AssertSameShape<Features.Get.NotificationTemplate, NotificationTemplateExt>();

    [Fact]
    public void GetAttachmentStatus_MatchesTheGeneratedEnum() =>
        AssertSameShape<Features.Get.AttachmentStatus, AttachmentStatusExt>();

    [Fact]
    public void GetAttachmentDataLocationType_MatchesTheGeneratedEnum() =>
        AssertSameShape<Features.Get.AttachmentDataLocationType, AttachmentDataLocationTypeExt>();

    [Fact]
    public void GetReferenceType_MatchesTheGeneratedEnum() =>
        AssertSameShape<Features.Get.ReferenceType, ReferenceTypeExt>();
}
