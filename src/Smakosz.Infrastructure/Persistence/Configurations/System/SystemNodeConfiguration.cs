using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence.Converters;

namespace Smakosz.Infrastructure.Persistence.Configurations.System;

public class SystemNodeConfiguration : IEntityTypeConfiguration<SystemNode>
{
    public void Configure(EntityTypeBuilder<SystemNode> builder)
    {
        builder.ToTable("nodes", "system");

        builder.HasKey(x => x.NodeId);

        builder.Property(x => x.NodeId)
            .HasMaxLength(50);

        builder.Property(x => x.IpAddress)
            .HasMaxLength(45);

        builder.Property(x => x.MacAddress)
            .HasMaxLength(17);

        builder.Property(x => x.WolGatewayId)
            .HasMaxLength(50);

        builder.Property(x => x.Role)
            .HasConversion(new SnakeCaseEnumConverter<NodeRole>())
            .HasMaxLength(20);

        builder.Property(x => x.Status)
            .HasMaxLength(20)
            .HasDefaultValue("offline");

        builder.Property(x => x.NodeType)
            .HasConversion(new SnakeCaseEnumConverter<NodeType>())
            .HasMaxLength(20);

        builder.Property(x => x.Hostname)
            .HasMaxLength(255);

        builder.Property(x => x.GpuName)
            .HasMaxLength(100);

        builder.Property(x => x.Metadata)
            .HasColumnType("jsonb");

        builder.HasOne(x => x.WolGateway)
            .WithMany()
            .HasForeignKey(x => x.WolGatewayId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.NodeType, x.Status, x.LastHeartbeat });
    }
}
