using System;

namespace NET6.Microservice.Messages.Events
{
    public class Notification
    {
        Guid NotificationId { get; set; }

        string NotificationType { get; set; }

        string NotificationContent { get; set; }

        string NotificationAddress { get; set; }

        DateTime NotificationDate { get; set; }
    }
}