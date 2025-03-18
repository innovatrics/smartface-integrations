using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Innovatrics.SmartFace.StoreNotifications.Data
{
    public class MainDbContext : DbContext
    {
        public DbSet<PedestrianProcessed> PedestrianProcessed { get; set; }
        public DbSet<MatchResult> MatchResult { get; set; }

        public MainDbContext(DbContextOptions<MainDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PedestrianProcessed>(entity =>
            {
                entity.ToTable("pedestrians_processed");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.StreamId).HasColumnName("stream_id");
                entity.Property(e => e.TrackletId).HasColumnName("tracklet_id");
                entity.Property(e => e.FrameId).HasColumnName("frame_id");
                entity.Property(e => e.FrameTimestampMicroseconds).HasColumnName("frame_timestamp_microseconds");
                entity.Property(e => e.ProcessedAt).HasColumnName("processed_at");
                entity.Property(e => e.Size).HasColumnName("size");
                entity.Property(e => e.Quality).HasColumnName("quality");
                entity.Property(e => e.PedestrianOrder).HasColumnName("pedestrian_order");
                entity.Property(e => e.PedestriansOnFrameCount).HasColumnName("pedestrians_on_frame_count");
            });

            modelBuilder.Entity<MatchResult>(entity =>
            {
                entity.ToTable("match_results");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.StreamId).HasColumnName("stream_id");
                entity.Property(e => e.FrameId).HasColumnName("frame_id");
                entity.Property(e => e.TrackletId).HasColumnName("tracklet_id");
                entity.Property(e => e.WatchlistId).HasColumnName("watchlist_id");
                entity.Property(e => e.WatchlistMemberId).HasColumnName("watchlist_member_id");
                entity.Property(e => e.WatchlistMemberDisplayName).HasColumnName("watchlist_member_display_name");
                entity.Property(e => e.FaceSize).HasColumnName("face_size");
                entity.Property(e => e.FaceOrder).HasColumnName("face_order");
                entity.Property(e => e.FacesOnFrameCount).HasColumnName("faces_on_frame_count");
                entity.Property(e => e.FaceQuality).HasColumnName("face_quality");
            });
        }
    }
}
