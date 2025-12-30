using EllipticCurve.Utils;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace E_commerce.Models.Entities
{
    public class ApplicationUser : IdentityUser
    {
        // Informations personnelles
        [PersonalData]
        [Display(Name = "Prénom")]
        [StringLength(50)]
        public string? FirstName { get; set; }

        [PersonalData]
        [Display(Name = "Nom")]
        [StringLength(50)]
        public string? LastName { get; set; }

        [PersonalData]
        [Display(Name = "Date de naissance")]
        public DateTime? DateOfBirth { get; set; }

        [PersonalData]
        [Display(Name = "Genre")]
        public Gender? Gender { get; set; }

        // Adresses
        [PersonalData]
        [Display(Name = "Adresse de livraison")]
        public Address? ShippingAddress { get; set; }

        [PersonalData]
        [Display(Name = "Adresse de facturation")]
        public Address? BillingAddress { get; set; }

        // Préférences
        [Display(Name = "Newsletter")]
        public bool SubscribeToNewsletter { get; set; } = true;

        [Display(Name = "Offres promotionnelles")]
        public bool AcceptMarketingEmails { get; set; } = true;

        [Display(Name = "Langue préférée")]
        public string PreferredLanguage { get; set; } = "fr-FR";

        [Display(Name = "Devise préférée")]
        public string PreferredCurrency { get; set; } = "EUR";

        // Compte et statistiques
        [Display(Name = "Date de création")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Dernière connexion")]
        public DateTime? LastLogin { get; set; }

        [Display(Name = "Statut du compte")]
        public AccountStatus AccountStatus { get; set; } = AccountStatus.Active;

        [Display(Name = "Niveau du compte")]
        public AccountTier AccountTier { get; set; } = AccountTier.Standard;

        [Display(Name = "Points de fidélité")]
        public int LoyaltyPoints { get; set; } = 0;

        [Display(Name = "Montant total dépensé")]
        [DataType(DataType.Currency)]
        public decimal TotalAmountSpent { get; set; } = 0;

        [Display(Name = "Nombre de commandes")]
        public int TotalOrders { get; set; } = 0;

        // Informations commerciales
        [Display(Name = "Type de client")]
        public CustomerType CustomerType { get; set; } = CustomerType.Consumer;

        [Display(Name = "Société")]
        [StringLength(100)]
        public string? CompanyName { get; set; }

        [Display(Name = "Numéro de TVA")]
        [StringLength(50)]
        public string? VATNumber { get; set; }

        // Préférences de shopping
        [Display(Name = "Catégories préférées")]
        public List<string> PreferredCategories { get; set; } = new();

        [Display(Name = "Marques préférées")]
        public List<string> PreferredBrands { get; set; } = new();

        [Display(Name = "Budget moyen")]
        [DataType(DataType.Currency)]
        public decimal? AverageBudget { get; set; }

        // Métadonnées
        [Display(Name = "Source d'inscription")]
        public string? RegistrationSource { get; set; } // "Website", "MobileApp", "SocialMedia"

        [Display(Name = "Dernière mise à jour")]
        public DateTime? LastProfileUpdate { get; set; }

        [Display(Name = "IP d'inscription")]
        public string? RegistrationIP { get; set; }

        // Relations
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        public virtual ICollection<WishlistItem> Wishlist { get; set; } = new List<WishlistItem>();
        public virtual ICollection<UserSearchHistory> SearchHistory { get; set; } = new List<UserSearchHistory>();
        public virtual ICollection<NotificationPreference> NotificationPreferences { get; set; } = new List<NotificationPreference>();
    }

    // Classes et enums de support
    public class Address
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Street { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string PostalCode { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Country { get; set; } = "France";

        [StringLength(100)]
        public string? State { get; set; }

        [StringLength(100)]
        public string? Apartment { get; set; }

        [StringLength(200)]
        public string? Landmark { get; set; }

        [Display(Name = "Adresse par défaut")]
        public bool IsDefault { get; set; }

        [StringLength(100)]
        public string? Label { get; set; } // "Maison", "Bureau", etc.

        [StringLength(50)]
        public string? PhoneNumber { get; set; }

        [StringLength(150)]
        public string? RecipientName { get; set; }
    }

    public class UserSearchHistory
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string SearchQuery { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
        public int ResultCount { get; set; }
        public bool ClickedResult { get; set; }

        public virtual ApplicationUser? User { get; set; }
    }

    public class NotificationPreference
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public bool EmailEnabled { get; set; } = true;
        public bool PushEnabled { get; set; } = true;
        public bool SMSEnabled { get; set; } = false;
        public string? PreferredTime { get; set; }

        public virtual ApplicationUser? User { get; set; }
    }

    public enum Gender
    {
        Male,
        Female,
        Other,
        PreferNotToSay
    }

    public enum AccountStatus
    {
        Active,
        Inactive,
        Suspended,
        Banned,
        PendingVerification
    }

    public enum AccountTier
    {
        Standard,
        Premium,
        Gold,
        Platinum,
        VIP
    }

    public enum CustomerType
    {
        Consumer,
        Business,
        Wholesale,
        Reseller
    }

    public enum NotificationType
    {
        OrderUpdates,
        ShippingNotifications,
        PromotionalOffers,
        PriceDrops,
        StockAlerts,
        NewArrivals,
        ReviewReminders,
        SecurityAlerts
    }
}