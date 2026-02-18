using Postgrest.Attributes;
using Postgrest.Models;
using System;

/*
 * DATABASE MODELS
 * * WHAT IT DOES:
 * - Defines C# classes that match your Supabase database tables.
 */
namespace Kumpas.Models
{
    // Maps to your 'profiles' table (Based on your Data Dictionary)
    [Table("profiles")]
    public class Profile : BaseModel
    {
        [PrimaryKey("id", false)] // `id` is the Primary Key
        public string Id { get; set; }

        [Column("first_name")]
        public string FirstName { get; set; }

        [Column("last_name")]
        public string LastName { get; set; }

        [Column("user_type")]
        public string UserType { get; set; } // "User" or "Admin"

        [Column("is_active")]
        public bool IsActive { get; set; }

        // FIX: Make nullable (?) so it's not sent on Insert, allowing DB default now()
        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }
    }

    // Maps to your 'chat_sessions' table
    [Table("chat_sessions")]
    public class ChatSession : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        [Column("user_1_id")]
        public string User1Id { get; set; }

        [Column("user_2_id")]
        public string User2Id { get; set; }

        [Column("user_1_deleted")]
        public bool User1Deleted { get; set; } = false;

        [Column("user_2_deleted")]
        public bool User2Deleted { get; set; } = false;

        [Column("room_code")]
        public string RoomCode { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }
    }

    [Table("chat_messages")]
    public class ChatMessage : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        [Column("session_id")]
        public string SessionId { get; set; }

        [Column("sender_id")]
        public string SenderId { get; set; }

        [Column("message_content")]
        public string MessageContent { get; set; }

        // Message types:
        // - "TEXT_TO_SIGN": Speech user sent text, triggers camera for Sign user
        // - "TEXT_TO_SPEECH": Sign user sent text, triggers TTS for Speech user
        // - "SIGN_VIDEO": Sign user sent video/sign language (future use)
        // - "SPEECH_AUDIO": Speech user sent audio (future use)
        [Column("message_type")]
        public string MessageType { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }
    }
}