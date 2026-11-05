using InsuranceBrokerage.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace InsuranceBrokerage.Application.dto
{
    public class CreateInsuranceApplicationRequest
    {
        [Range(1, int.MaxValue)]
        public int LeadId { get; set; }

        [EnumDataType(typeof(InsuranceType))]
        public InsuranceType InsuranceType { get; set; }

        [Range(typeof(decimal), "0.01", "9999999999999999")]
        public decimal CoverageAmount { get; set; }

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "AED";

        [StringLength(1000)]
        public string? Description { get; set; }
    }
}
