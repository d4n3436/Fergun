﻿// <auto-generated />
using Fergun.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Fergun.Data.Migrations
{
    [DbContext(typeof(FergunContext))]
    [Migration("20240904021127_AddMaxLengths")]
    partial class AddMaxLengths
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.7");

            modelBuilder.Entity("Fergun.Data.Models.Command", b =>
                {
                    b.Property<string>("Name")
                        .HasMaxLength(32)
                        .HasColumnType("TEXT");

                    b.Property<int>("UsageCount")
                        .HasColumnType("INTEGER");

                    b.HasKey("Name");

                    b.ToTable("CommandStats");
                });

            modelBuilder.Entity("Fergun.Data.Models.User", b =>
                {
                    b.Property<ulong>("Id")
                        .HasColumnType("INTEGER");

                    b.Property<string>("BlacklistReason")
                        .HasMaxLength(256)
                        .HasColumnType("TEXT");

                    b.Property<int>("BlacklistStatus")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("Users");
                });
#pragma warning restore 612, 618
        }
    }
}