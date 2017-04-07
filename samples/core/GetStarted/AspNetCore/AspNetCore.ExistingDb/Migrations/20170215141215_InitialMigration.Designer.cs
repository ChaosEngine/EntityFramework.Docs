﻿using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using EFGetStarted.AspNetCore.ExistingDb.Models;

namespace AspNetCore.ExistingDb.Migrations
{
    [DbContext(typeof(BloggingContext))]
    [Migration("20170215141215_InitialMigration")]
    partial class InitialMigration
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.1.0-rtm-22752")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("EFGetStarted.AspNetCore.ExistingDb.Models.Blog", b =>
                {
                    b.Property<int>("BlogId")
                        .ValueGeneratedOnAdd()
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>("Url")
                        .IsRequired();

                    b.HasKey("BlogId");

                    b.ToTable("Blog");
                });

            modelBuilder.Entity("EFGetStarted.AspNetCore.ExistingDb.Models.Post", b =>
                {
                    b.Property<int>("PostId")
                        .ValueGeneratedOnAdd()
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>("BlogId");

                    b.Property<string>("Content");

                    b.Property<string>("Title");

                    b.HasKey("PostId");

                    b.HasIndex("BlogId");

                    b.ToTable("Post");
                });

            modelBuilder.Entity("EFGetStarted.AspNetCore.ExistingDb.Models.ThinHashes", b =>
                {
                    b.Property<string>("Key")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("varchar(20)");

                    b.Property<string>("HashMD5")
                        .IsRequired()
                        .HasColumnName("hashMD5")
                        .HasColumnType("char(32)");

                    b.Property<string>("HashSHA256")
                        .IsRequired()
                        .HasColumnName("hashSHA256")
                        .HasColumnType("char(64)");

                    b.HasKey("Key");

                    b.ToTable("Hashes");
                });

            modelBuilder.Entity("EFGetStarted.AspNetCore.ExistingDb.Models.Post", b =>
                {
                    b.HasOne("EFGetStarted.AspNetCore.ExistingDb.Models.Blog", "Blog")
                        .WithMany("Post")
                        .HasForeignKey("BlogId")
                        .OnDelete(DeleteBehavior.Cascade);
                });
        }
    }
}