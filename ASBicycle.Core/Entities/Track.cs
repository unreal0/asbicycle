﻿using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abp.Domain.Entities;

namespace ASBicycle.Entities
{
    [Table("Track")]
    public class Track : Entity
    {
        public DateTime? Created_at { get; set; }
        public DateTime? Updated_at { get; set; }
        [MaxLength(100)]
        public string Start_point { get; set; }
        [MaxLength(100)]
        public string End_point { get; set; }
        public int? Start_site_id { get; set; }
        public int? End_site_id { get; set; }
        public DateTime Start_time { get; set; }
        public DateTime End_time { get; set; }
        public int? Payment { get; set; }
        public int? Pay_status { get; set; }


        public int User_id { get; set; }
        public int? Bike_id { get; set; }
        //[ForeignKey("User_id")]
        //public virtual User User { get; set; }
        [ForeignKey("Bike_id")]
        public virtual Bike Bike { get; set; }

    }
}