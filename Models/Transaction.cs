
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

namespace MyProject12.Models
{
    [Table("Transactions")]
        public class Transaction
        {
            [Key]
        public int ID { get; set; }  // ID property is not optional

        public DateTime Created { get; set; }

        public string Status { get; set; }
        public int? StatusCode { get; set; }

        public int? TransactionId { get; set; }

        public string TransactionToken { get; set; }

        public int? TransactionTypeId { get; set; }

        public int? PaymentType { get; set; }

        public float? Sum { get; set; }

        public float? FirstPaymentSum { get; set; }

        public float? PeriodicalPaymentSum { get; set; }

        public int? PaymentsNum { get; set; }

        public int? AllPaymentsNum { get; set; }

        public string PaymentDate { get; set; }

        public string Asmachta { get; set; }

        public string Description { get; set; }

        public string FullName { get; set; }

        public string PayerPhone { get; set; }

        public string PayerEmail { get; set; }

        public string CardSuffix { get; set; }

        public string CardType { get; set; }

        public int? CardTypeCode { get; set; }

        public string CardBrand { get; set; }

        public int? CardBrandCode { get; set; }

        public string CardExp { get; set; }

        public int? ProcessId { get; set; }

        public string ProcessToken { get; set; }

        public string CardToken { get; set; }

        public int? DirectDebitId { get; set; }

    }
}