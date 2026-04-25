using System;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly string connectionString;

        public NotificationRepository(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public void AddNotification(int recipientStaffId, string title, string message)
        {
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand(@"
                INSERT INTO Notifications (recipient_staff_id, title, message, created_at, is_read)
                VALUES (@RecipientStaffId, @Title, @Message, @CreatedAt, 0);", connection);
            command.Parameters.Add(new SqlParameter("@RecipientStaffId", recipientStaffId));
            command.Parameters.Add(new SqlParameter("@Title", title));
            command.Parameters.Add(new SqlParameter("@Message", message));
            command.Parameters.Add(new SqlParameter("@CreatedAt", DateTime.UtcNow));
            command.ExecuteNonQuery();
        }
    }
}
