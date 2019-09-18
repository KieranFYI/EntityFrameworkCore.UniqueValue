using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.UniqueValue.Attributes
{
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
	public class UniqueAttribute : ValidationAttribute
	{
		public UniqueAttribute(string groupId = null)
		{
			GroupId = groupId;
		}

		public string GroupId { get; set; }

		protected override ValidationResult IsValid(object value, ValidationContext validationContext)
		{
			var context = validationContext.GetService(typeof(UniqueValueDbContext)) as UniqueValueDbContext;
			var set = (IQueryable<object>) context.GetType().GetMethod("Set").MakeGenericMethod(validationContext.ObjectType).Invoke(context, null);
			var validationObject = validationContext.ObjectInstance;


			var entityType = context.Model.FindEntityType(validationObject.GetType().ToString());
			var check = set;


			var keys = entityType.FindPrimaryKey().Properties;
			foreach (var key in keys)
			{
				var prop = validationObject.GetType()
					.GetProperty(key.Name);
				var val = prop.GetValue(validationObject);
				check = check.Where(s => prop.GetValue(s) != null && !prop.GetValue(s).Equals(val));
			}

			var entityProps = entityType.GetProperties();
			var checkedProps = new List<IProperty>();
			foreach (IProperty propToCheck in entityProps)
			{
				var uniqueAttributes = UniqueValueDbContext.GetAttributes<UniqueAttribute>(entityType, propToCheck);
				if (!uniqueAttributes.Any())
				{
					continue;
				}

				if (uniqueAttributes.First().GroupId != GroupId)
				{
					continue;
				}

				if (propToCheck.IsShadowProperty && !propToCheck.GetContainingForeignKeys().Where(fk => fk.DependentToPrincipal != null && fk.DependentToPrincipal.Name != null).Any())
				{
					return null;
				}

				checkedProps.Add(propToCheck);

				if (propToCheck.IsShadowProperty)
				{
					var includeProp = propToCheck.GetContainingForeignKeys()
						.FirstOrDefault(fk => fk.DependentToPrincipal != null && fk.DependentToPrincipal.Name != null)
						.DependentToPrincipal;
					check = check.Include(includeProp.Name);
					var typeProp = validationObject.GetType().GetProperty(includeProp.Name);
					var typePropValue = typeProp.GetValue(validationObject);
					var keyProp = typePropValue.GetType().GetProperty(propToCheck.Name);
					var keyPropValue = keyProp.GetValue(typePropValue);
					var v = keyProp.GetValue(typeProp.GetValue(check.First()));
					check = check.Where(s => typeProp.GetValue(s) != null && keyProp.GetValue(typeProp.GetValue(s)).Equals(keyPropValue));
				}
				else
				{
					var prop = validationObject.GetType()
								.GetProperty(propToCheck.PropertyInfo.Name);
					var validationValue = prop.GetValue(validationObject);
					if (validationValue is string validationValueString)
					{
						check = check.Where(s =>
							prop.GetValue(s)
								 .ToString()
								.Equals(
									validationValueString, StringComparison.OrdinalIgnoreCase
								)
						);
					}
					else
					{
						check = check.Where(s =>
							prop.GetValue(s)
								.Equals(validationValue)
						);
					}
				}

			}

			if (check.Any())
			{
				return new ValidationResult("The field must be unique.", checkedProps.Select(p => p.Name).ToArray());
			}

			return ValidationResult.Success;
		}
	}
}