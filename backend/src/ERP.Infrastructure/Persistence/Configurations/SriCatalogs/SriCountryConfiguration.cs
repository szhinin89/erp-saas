using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public class SriCountryConfiguration : IEntityTypeConfiguration<SriCountry>
{
    public void Configure(EntityTypeBuilder<SriCountry> builder)
    {
        builder.ToTable("sri_country", schema: "global");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(3).IsFixedLength();
        builder
            .Property(x => x.Iso2)
            .HasColumnName("iso2")
            .HasMaxLength(2)
            .IsFixedLength()
            .IsRequired();
        builder.HasAlternateKey(x => x.Iso2);
        // Código numérico SRI (ficha 2.34 Tabla 25, igual a Catálogo ATS 06/08/2026 Tabla 16).
        builder.Property(x => x.SriCode).HasColumnName("sri_code").HasMaxLength(3).IsFixedLength();
        builder.HasIndex(x => x.SriCode).IsUnique().HasDatabaseName("uq_sri_country_sri_code");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.PhoneCode).HasColumnName("phone_code").HasMaxLength(10);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder.HasData(
            new SriCountry
            {
                Code = "ECU",
                Iso2 = "EC",
                SriCode = "593",
                Name = "ECUADOR",
                PhoneCode = "+593",
            },
            new SriCountry
            {
                Code = "USA",
                Iso2 = "US",
                SriCode = "110",
                Name = "ESTADOS UNIDOS",
                PhoneCode = "+1",
            },
            new SriCountry
            {
                Code = "COL",
                Iso2 = "CO",
                SriCode = "105",
                Name = "COLOMBIA",
                PhoneCode = "+57",
            },
            new SriCountry
            {
                Code = "PER",
                Iso2 = "PE",
                SriCode = "120",
                Name = "PERÚ",
                PhoneCode = "+51",
            },
            new SriCountry
            {
                Code = "BOL",
                Iso2 = "BO",
                SriCode = "102",
                Name = "BOLIVIA",
                PhoneCode = "+591",
            },
            new SriCountry
            {
                Code = "CHL",
                Iso2 = "CL",
                SriCode = "108",
                Name = "CHILE",
                PhoneCode = "+56",
            },
            new SriCountry
            {
                Code = "ARG",
                Iso2 = "AR",
                SriCode = "101",
                Name = "ARGENTINA",
                PhoneCode = "+54",
            },
            new SriCountry
            {
                Code = "BRA",
                Iso2 = "BR",
                SriCode = "103",
                Name = "BRASIL",
                PhoneCode = "+55",
            },
            new SriCountry
            {
                Code = "MEX",
                Iso2 = "MX",
                SriCode = "116",
                Name = "MÉXICO",
                PhoneCode = "+52",
            },
            new SriCountry
            {
                Code = "VEN",
                Iso2 = "VE",
                SriCode = "126",
                Name = "VENEZUELA",
                PhoneCode = "+58",
            },
            new SriCountry
            {
                Code = "PAN",
                Iso2 = "PA",
                SriCode = "118",
                Name = "PANAMÁ",
                PhoneCode = "+507",
            },
            new SriCountry
            {
                Code = "CRI",
                Iso2 = "CR",
                SriCode = "106",
                Name = "COSTA RICA",
                PhoneCode = "+506",
            },
            new SriCountry
            {
                Code = "GTM",
                Iso2 = "GT",
                SriCode = "111",
                Name = "GUATEMALA",
                PhoneCode = "+502",
            },
            new SriCountry
            {
                Code = "HND",
                Iso2 = "HN",
                SriCode = "113",
                Name = "HONDURAS",
                PhoneCode = "+504",
            },
            new SriCountry
            {
                Code = "NIC",
                Iso2 = "NI",
                SriCode = "117",
                Name = "NICARAGUA",
                PhoneCode = "+505",
            },
            new SriCountry
            {
                Code = "SLV",
                Iso2 = "SV",
                SriCode = "123",
                Name = "EL SALVADOR",
                PhoneCode = "+503",
            },
            new SriCountry
            {
                Code = "DOM",
                Iso2 = "DO",
                SriCode = "122",
                Name = "REPÚBLICA DOMINICANA",
                PhoneCode = "+1",
            },
            new SriCountry
            {
                Code = "URY",
                Iso2 = "UY",
                SriCode = "125",
                Name = "URUGUAY",
                PhoneCode = "+598",
            },
            new SriCountry
            {
                Code = "PRY",
                Iso2 = "PY",
                SriCode = "119",
                Name = "PARAGUAY",
                PhoneCode = "+595",
            },
            new SriCountry
            {
                Code = "ESP",
                Iso2 = "ES",
                SriCode = "209",
                Name = "ESPAÑA",
                PhoneCode = "+34",
            },
            new SriCountry
            {
                Code = "DEU",
                Iso2 = "DE",
                SriCode = "202",
                Name = "ALEMANIA",
                PhoneCode = "+49",
            },
            new SriCountry
            {
                Code = "ITA",
                Iso2 = "IT",
                SriCode = "219",
                Name = "ITALIA",
                PhoneCode = "+39",
            },
            new SriCountry
            {
                Code = "FRA",
                Iso2 = "FR",
                SriCode = "211",
                Name = "FRANCIA",
                PhoneCode = "+33",
            },
            new SriCountry
            {
                Code = "GBR",
                Iso2 = "GB",
                SriCode = "213",
                Name = "REINO UNIDO",
                PhoneCode = "+44",
            },
            new SriCountry
            {
                Code = "JPN",
                Iso2 = "JP",
                SriCode = "314",
                Name = "JAPÓN",
                PhoneCode = "+81",
            },
            new SriCountry
            {
                Code = "CHN",
                Iso2 = "CN",
                SriCode = "331",
                Name = "CHINA",
                PhoneCode = "+86",
            },
            new SriCountry
            {
                Code = "IND",
                Iso2 = "IN",
                SriCode = "309",
                Name = "INDIA",
                PhoneCode = "+91",
            },
            new SriCountry
            {
                Code = "CAN",
                Iso2 = "CA",
                SriCode = "104",
                Name = "CANADÁ",
                PhoneCode = "+1",
            },
            new SriCountry
            {
                Code = "AUS",
                Iso2 = "AU",
                SriCode = "501",
                Name = "AUSTRALIA",
                PhoneCode = "+61",
            }
        );
    }
}
