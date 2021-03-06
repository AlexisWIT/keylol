﻿using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Keylol.Models
{
    public class SteamBindingToken
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Index(IsUnique = true, IsClustered = true)]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public int Sid { get; set; }

        [Required]
        [MaxLength(8)]
        [Index(IsUnique = true)]
        public string Code { get; set; }

        [Required]
        [Index]
        [MaxLength(128)]
        public string BrowserConnectionId { get; set; }

        [MaxLength(64)]
        [Index]
        public string SteamId { get; set; }

        public string BotId { get; set; }

        public virtual SteamBot Bot { get; set; }
    }
}