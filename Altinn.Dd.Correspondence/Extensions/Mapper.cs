using Altinn.Dd.Correspondence.HttpClients;
using Altinn.Dd.Correspondence.Models;

namespace Altinn.Dd.Correspondence.Extensions;

internal static class Mapper
{
    extension(InitializeCorrespondencesResponseExt e)
    {
        public InitializedCorrespondences ToDto()
        {
            return new InitializedCorrespondences(
                Correspondences: [.. e.Correspondences.Select(ToDto)],
                AttachmentIds: e.AttachmentIds
            );
        }
    }

    extension(InitializedCorrespondencesExt e)
    {
        public InitializedCorrespondence ToDto()
        {
            return new InitializedCorrespondence(
                Status: (CorrespondenceStatus)e.Status,
                Recipient: e.Recipient,
                Notifications: e.Notifications?.Select(ToDto)?.ToList()
            );
        }
    }

    extension(InitializedCorrespondencesNotificationsExt e)
    {
        public InitializedNotification ToDto()
        {
            return new InitializedNotification(
                OrderId: e.OrderId,
                IsReminder: e.IsReminder,
                Status: (InitializedNotificationStatus)e.Status);
        }
    }
}
