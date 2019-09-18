using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Linq;
using MemesAPI.DbContext.Attributes;

namespace EntityFrameworkCore.UniqueValue
{
    public class UniqueValueDbContext : DbContext
    {
        public UniqueValueDbContext()
        {
        }

        public UniqueValueDbContext(DbContextOptions<UniqueValueDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

			// Iterate through all EF Entity types
			foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                #region Convert UniqueKeyAttribute on Entities to UniqueKey in DB
                var properties = entityType.GetProperties();
                if ((properties != null) && (properties.Any()))
                {
                    foreach (var property in properties)
                    {
                        IEnumerable<UniqueAttribute> uniqueKeys = GetAttributes<UniqueAttribute>(entityType, property);
                        if (uniqueKeys != null && uniqueKeys.Any())
                        {
                            uniqueKeys = uniqueKeys.GroupBy(u => u.GroupId).Select(x => x.First()).ToArray();
                            foreach (var uniqueKey in uniqueKeys)
                            {

                                var mutableProperties = new List<IMutableProperty>();
                                properties.ToList().ForEach(x =>
                                {
                                    var uks = GetAttributes<UniqueAttribute>(entityType, x);
                                    if (uks != null)
                                    {
                                        foreach (var uk in uks)
                                        {
                                            if ((uk != null) && (uk.GroupId == uniqueKey.GroupId))
                                            {
                                                mutableProperties.Add(x);
                                            }
                                        }
                                    }
                                });
                                entityType.GetOrAddIndex(mutableProperties).IsUnique = true;
                            }
                        }
                    }
                }
                #endregion Convert UniqueKeyAttribute on Entities to UniqueKey in DB
            }
        }

        public static IEnumerable<T> GetAttributes<T>(IEntityType entityType, IProperty property) where T : Attribute
        {
            if (entityType == null)
            {
                throw new ArgumentNullException(nameof(entityType));
            }
            else if (entityType.ClrType == null)
            {
                throw new ArgumentNullException(nameof(entityType.ClrType));
            }
            else if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
			}
			else if (property.IsShadowProperty && !property.GetContainingForeignKeys().Where(fk => fk.DependentToPrincipal != null && fk.DependentToPrincipal.Name != null).Any())
			{
				return null;
			}
			var propInfo = entityType.ClrType.GetProperty(
				property.IsShadowProperty
					? property.GetContainingForeignKeys().Where(fk => fk.DependentToPrincipal != null && fk.DependentToPrincipal.Name != null).FirstOrDefault().DependentToPrincipal.Name
					: property.PropertyInfo.Name,
                BindingFlags.NonPublic |
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly);
			if (propInfo == null)
			{
				return null;
			}
            return propInfo.GetCustomAttributes<T>();
        }
    }

}
