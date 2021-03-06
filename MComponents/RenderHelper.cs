﻿using MComponents.InputElements;
using MComponents.MForm;
using MComponents.MSelect;
using MComponents.Shared.Attributes;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.CompilerServices;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace MComponents
{
    public static class RenderHelper
    {
        private static Type[] mNumberTypes = { typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) };

        private static List<Type> mSupportedTypes = new List<Type>();

        static RenderHelper()
        {
            mSupportedTypes.AddRange(mNumberTypes);
            mSupportedTypes.Add(typeof(string));
            mSupportedTypes.Add(typeof(DateTime));
            mSupportedTypes.Add(typeof(bool));
            mSupportedTypes.Add(typeof(Guid));
        }

        public static bool IsTypeSupported(Type pType)
        {
            if (pType.IsEnum)
                return true;

            Type nullableType = Nullable.GetUnderlyingType(pType);
            if (nullableType != null)
            {
                return IsTypeSupported(nullableType);
            }

            return mSupportedTypes.Contains(pType);
        }

        public static void AppendInput<T>(RenderTreeBuilder pBuilder, IMPropertyInfo pPropertyInfo, object pModel, Guid pId, IMForm pParent, bool pIsInFilterRow, IMField pField)
        {
            try
            {
                if (!IsTypeSupported(typeof(T)) || IsPropertyHolderNull(pPropertyInfo, pModel))
                {
                    ShowNotSupportedType(pBuilder, pPropertyInfo, pModel, pId, pParent);
                    return;
                }

                T value = (T)(pPropertyInfo.GetValue(pModel) ?? default(T));
                Type tType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

                bool isReadOnly = pPropertyInfo.IsReadOnly || pPropertyInfo.GetCustomAttribute(typeof(ReadOnlyAttribute)) != null;

                if (mNumberTypes.Contains(tType))
                {
                    pBuilder.OpenComponent<InputNumber<T>>(0);
                }
                else if (tType == typeof(DateTime) || tType == typeof(DateTimeOffset))
                {
                    if (pPropertyInfo.GetCustomAttribute(typeof(TimeAttribute)) != null)
                    {
                        pBuilder.OpenComponent<InputTime<T>>(0);
                    }
                    else if (pPropertyInfo.GetCustomAttribute(typeof(DateTimeAttribute)) != null)
                    {
                        pBuilder.OpenComponent<InputDateTime<T>>(0);
                    }
                    else
                    {
                        pBuilder.OpenComponent<InputDate<T>>(0);
                    }
                }
                else if (typeof(T) == typeof(bool))
                {
                    pBuilder.OpenComponent<MInputCheckbox>(0);
                }
                else if (typeof(T) == typeof(bool?))
                {
                    pBuilder.OpenComponent<MSelect<T>>(0);
                    if (pIsInFilterRow)
                        pBuilder.AddAttribute(10, "NullValueDescription", "\u200b");
                }
                else if (tType == typeof(Guid))
                {
                    pBuilder.OpenComponent<InputGuid<T>>(0);
                }
                else if (tType.IsEnum)
                {
                    pBuilder.OpenComponent<MSelect<T>>(0);
                    if (pIsInFilterRow)
                        pBuilder.AddAttribute(10, "NullValueDescription", "\u200b");
                }
                else
                {
                    if (pPropertyInfo.GetCustomAttribute(typeof(TextAreaAttribute)) != null)
                    {
                        pBuilder.OpenComponent<InputTextArea>(0);
                    }
                    else
                    {
                        pBuilder.OpenComponent<InputText>(0);
                    }
                }

                if (pPropertyInfo.GetCustomAttribute(typeof(PasswordAttribute)) != null)
                {
                    pBuilder.AddAttribute(33, "type", "password");
                }

                if (pField.AdditionalAttributes != null)
                    pBuilder.AddMultipleAttributes(17, pField.AdditionalAttributes
                        .Where(a => a.Key != Extensions.MFORM_IN_TABLE_ROW_TD_STYLE_ATTRIBUTE)
                        .ToDictionary(a => a.Key, a => a.Value));

                pBuilder.AddAttribute(1, "id", pId);
                pBuilder.AddAttribute(2, "Value", value);

                pBuilder.AddAttribute(23, "ValueChanged", RuntimeHelpers.CreateInferredEventCallback<T>(pParent, __value =>
                {
                    pPropertyInfo.SetValue(pModel, __value);
                    pParent.OnInputValueChanged(pPropertyInfo.Name, __value);
                }, value));

                pBuilder.AddAttribute(23, "onkeyup", EventCallback.Factory.Create<KeyboardEventArgs>(pParent, (a) =>
                {
                    pParent.OnInputKeyUp(a);
                }));

                var valueExpression = GetValueExpression<T>(pPropertyInfo, pModel);

                pBuilder.AddAttribute(4, "ValueExpression", valueExpression);

                string cssClass = "m-form-control";

                if (isReadOnly)
                {
                    pBuilder.AddAttribute(33, "disabled", string.Empty);
                    pBuilder.AddAttribute(33, "IsDisabled", true);
                }

                pBuilder.AddAttribute(10, "class", cssClass);

                if (typeof(T) == typeof(bool?))
                {
                    IEnumerable<bool?> options = new bool?[] { true, false };
                    pBuilder.AddAttribute(10, "Options", options);
                }

                pBuilder.CloseComponent();

                if (pParent.EnableValidation)
                {
                    pBuilder.OpenComponent<ValidationMessage<T>>(60);
                    pBuilder.AddAttribute(61, "For", valueExpression);
                    pBuilder.CloseComponent();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                throw;
            }
        }

        public static void AppendComplexType<T, TProperty>(RenderTreeBuilder pBuilder, IMPropertyInfo pPropertyInfo, T pModel, Guid pId, IMForm pParent, MComplexPropertyField<T, TProperty> pComplexField,
            MFormGridContext pGridContext)
        {
            if (pComplexField.Template == null)
            {
                ShowNotSupportedType(pBuilder, pPropertyInfo, pModel, pId, pParent);
                return;
            }

            MComplexPropertyFieldContext<TProperty> context = new MComplexPropertyFieldContext<TProperty>();

            TProperty value = (TProperty)pPropertyInfo.GetValue(pModel);

#pragma warning disable BL0005 // Component parameter should not be set outside of its component.
            context.Row = pModel;
            context.InputId = pId;
            context.Value = value;
            context.MFormGridContext = pGridContext;

            context.ValueChanged = RuntimeHelpers.CreateInferredEventCallback<TProperty>(pParent, __value =>
            {
                pPropertyInfo.SetValue(pModel, __value);
                pParent.OnInputValueChanged(pPropertyInfo.Name, __value);
            }, value);

            context.ValueExpression = GetValueExpression<TProperty>(pPropertyInfo, pModel);

#pragma warning restore BL0005 // Component parameter should not be set outside of its component.

            pBuilder.AddContent(42, pComplexField.Template?.Invoke(context));
        }

        private static Expression<Func<T>> GetValueExpression<T>(IMPropertyInfo pPropertyInfo, object pModel)
        {
            if (pModel is IDictionary<string, object>)
            {
                var fake = new FakePropertyInfo<T>(pPropertyInfo.Name);

                //just create a member expression with random values
                MemberExpression expression = Expression.Property(Expression.Constant(fake), nameof(fake.CanRead));

                Expression constantExpression = Expression.Constant(default(T), typeof(T));

                var constantExpressionValueBaseFields = constantExpression.GetType().BaseType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
                var constantExpressionValueFields = constantExpression.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

                var field = constantExpressionValueBaseFields.Concat(constantExpressionValueFields).First(f => f.FieldType == typeof(object));
                field.SetValue(constantExpression, pModel);

                //set generated constant expression
                var expressionField = expression.GetType().BaseType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic).First(f => f.FieldType == typeof(Expression));
                expressionField.SetValue(expression, constantExpression);

                //set fake property type
                var propertyField = expression.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic).First(f => f.FieldType == typeof(PropertyInfo));
                propertyField.SetValue(expression, fake);

                //now we have generated an MemberExpression which has the pModel as value and an FakePropertyInfo with correct type

                return Expression.Lambda<Func<T>>(expression);
            }
            else
            {
                var propertyholder = pPropertyInfo.GetPropertyHolder(pModel);
                return Expression.Lambda<Func<T>>(Expression.Property(Expression.Constant(propertyholder), pPropertyInfo.Name));
            }
        }

        private static bool IsPropertyHolderNull(IMPropertyInfo pPropertyInfo, object pModel)
        {
            if (pModel is IDictionary<string, object>)
            {
                return false;
            }

            return pPropertyInfo.GetPropertyHolder(pModel) == null;
        }


        public static void ShowNotSupportedType(RenderTreeBuilder pBuilder, IMPropertyInfo pPropertyInfo, object pModel, Guid pId, IMForm pParent)
        {
            var value = pPropertyInfo.GetValue(pModel);

            pBuilder.OpenElement(45, "input");
            pBuilder.AddAttribute(1, "id", pId);
            pBuilder.AddAttribute(2, "Value", value);
            pBuilder.AddAttribute(33, "disabled", string.Empty);
            pBuilder.AddAttribute(33, "class", "m-form-control");
            pBuilder.CloseElement();
        }
    }
}
