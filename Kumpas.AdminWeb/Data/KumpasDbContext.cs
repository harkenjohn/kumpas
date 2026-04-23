using Kumpas.AdminWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace Kumpas.AdminWeb.Data;

public class KumpasDbContext : DbContext
{
    public KumpasDbContext(DbContextOptions<KumpasDbContext> options) : base(options)
    {
    }

    public DbSet<AuthUser> AuthUsers => Set<AuthUser>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<GestureLibrary> GestureLibraries => Set<GestureLibrary>();
    public DbSet<ArModel> ArModels => Set<ArModel>();
    public DbSet<GestureRecognitionData> GestureRecognitionData => Set<GestureRecognitionData>();
    public DbSet<SystemLog> SystemLogs => Set<SystemLog>();
    public DbSet<ModelStatusLog> ModelStatusLogs => Set<ModelStatusLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AuthUser>(entity =>
        {
            entity.ToTable("users", "auth");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.Email)
                .HasColumnName("email");

            entity.Property(x => x.EncryptedPassword)
                .HasColumnName("encrypted_password");
        });

        modelBuilder.Entity<Profile>(entity =>
        {
            entity.ToTable("profiles");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.CreatedAt)
                .HasColumnName("created_at");

            entity.Property(x => x.FirstName)
                .HasColumnName("first_name");

            entity.Property(x => x.LastName)
                .HasColumnName("last_name");

            entity.Property(x => x.UserType)
                .HasColumnName("user_type");

            entity.Property(x => x.IsActive)
                .HasColumnName("is_active");

            entity.HasOne(x => x.AuthUser)
                .WithOne(x => x.Profile)
                .HasForeignKey<Profile>(x => x.Id)
                .HasPrincipalKey<AuthUser>(x => x.Id)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.ToTable("chat_sessions");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.CreatedAt)
                .HasColumnName("created_at");

            entity.Property(x => x.User1Id)
                .HasColumnName("user_1_id");

            entity.Property(x => x.User2Id)
                .HasColumnName("user_2_id");

            entity.Property(x => x.RoomCode)
                .HasColumnName("room_code");

            entity.Property(x => x.User1Deleted)
                .HasColumnName("user_1_deleted");

            entity.Property(x => x.User2Deleted)
                .HasColumnName("user_2_deleted");

            entity.HasOne(x => x.User1)
                .WithMany(x => x.ChatSessionsAsUser1)
                .HasForeignKey(x => x.User1Id)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.User2)
                .WithMany(x => x.ChatSessionsAsUser2)
                .HasForeignKey(x => x.User2Id)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("chat_messages");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.CreatedAt)
                .HasColumnName("created_at");

            entity.Property(x => x.SessionId)
                .HasColumnName("session_id");

            entity.Property(x => x.SenderId)
                .HasColumnName("sender_id");

            entity.Property(x => x.MessageContent)
                .HasColumnName("message_content");

            entity.Property(x => x.GestureId)
                .HasColumnName("gesture_id");

            entity.HasOne(x => x.ChatSession)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Sender)
                .WithMany(x => x.ChatMessages)
                .HasForeignKey(x => x.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Gesture)
                .WithMany(x => x.ChatMessages)
                .HasForeignKey(x => x.GestureId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GestureLibrary>(entity =>
        {
            entity.ToTable("gesture_library");

            entity.HasKey(x => x.GestureId);

            entity.Property(x => x.GestureId)
                .HasColumnName("gesture_id");

            entity.Property(x => x.GestureName)
                .HasColumnName("gesture_name");

            entity.Property(x => x.GestureType)
                .HasColumnName("gesture_type");

            entity.Property(x => x.Description)
                .HasColumnName("description");

            entity.Property(x => x.Category)
                .HasColumnName("category");
        });

        modelBuilder.Entity<ArModel>(entity =>
        {
            entity.ToTable("ar_models");

            entity.HasKey(x => x.ModelId);

            entity.Property(x => x.ModelId)
                .HasColumnName("model_id");

            entity.Property(x => x.GestureId)
                .HasColumnName("gesture_id");

            entity.Property(x => x.ModelFilePath)
                .HasColumnName("model_file_path");

            entity.Property(x => x.AnimationFilePath)
                .HasColumnName("animation_file_path");

            entity.HasOne(x => x.Gesture)
                .WithMany(x => x.ArModels)
                .HasForeignKey(x => x.GestureId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GestureRecognitionData>(entity =>
        {
            entity.ToTable("gesture_recognition_data");

            entity.HasKey(x => x.DataId);

            entity.Property(x => x.DataId)
                .HasColumnName("data_id");

            entity.Property(x => x.GestureId)
                .HasColumnName("gesture_id");

            entity.Property(x => x.ImagePath)
                .HasColumnName("image_path");

            entity.Property(x => x.VideoPath)
                .HasColumnName("video_path");

            entity.Property(x => x.KeypointData)
                .HasColumnName("keypoint_data")
                .HasColumnType("json");

            entity.HasOne(x => x.Gesture)
                .WithMany(x => x.RecognitionData)
                .HasForeignKey(x => x.GestureId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SystemLog>(entity =>
        {
            entity.ToTable("system_logs");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.UserId)
                .HasColumnName("user_id");

            entity.Property(x => x.LogLevel)
                .HasColumnName("log_level");

            entity.Property(x => x.Module)
                .HasColumnName("module");

            entity.Property(x => x.Message)
                .HasColumnName("message");

            entity.Property(x => x.ErrorStack)
                .HasColumnName("error_stack");

            entity.Property(x => x.Timestamp)
                .HasColumnName("timestamp");

            entity.HasOne(x => x.User)
                .WithMany(x => x.SystemLogs)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ModelStatusLog>(entity =>
        {
            entity.ToTable("model_status_logs");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.Status)
                .HasColumnName("status");

            entity.Property(x => x.RecordedAt)
                .HasColumnName("recorded_at");
        });
    }
}
