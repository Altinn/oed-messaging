using Altinn.Dd.Correspondence.HttpClients;
using Altinn.Dd.Correspondence.Models;

namespace Altinn.Dd.Correspondence.Extensions;

internal static class Mapper
{
    public static InitializedCorrespondences ToDto(this InitializeCorrespondencesResponseExt e)
    {
        return new InitializedCorrespondences(
            Correspondences: [.. e.Correspondences.Select(ToDto)],
            AttachmentIds: e.AttachmentIds
        );
    }

    public static InitializedCorrespondence ToDto(this InitializedCorrespondencesExt e)
    {
        return new InitializedCorrespondence(
            Status: (CorrespondenceStatus)e.Status,
            Recipient: e.Recipient,
            Notifications: e.Notifications?.Select(ToDto)?.ToList()
        );
    }

    public static InitializedNotification ToDto(this InitializedCorrespondencesNotificationsExt e)
    {
        return new InitializedNotification(
            OrderId: e.OrderId,
            IsReminder: e.IsReminder,
            Status: (InitializedNotificationStatus)e.Status);
    }
}
