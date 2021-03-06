﻿using MComponents.MForm;
using MComponents.Shared.Attributes;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace MComponents
{

    public class MForm<T> : ComponentBase, IMForm
    {
        protected EditContext mEditContext;

        [CascadingParameter]
        MFormContainerContext ContainerContext { get; set; }

        [Parameter(CaptureUnmatchedValues = true)]
        public IReadOnlyDictionary<string, object> AdditionalAttributes { get; set; }

        [Parameter]
        public T Model { get; set; }

        [Parameter]
        public bool IsInTableRow { get; set; }

        [Parameter]
        public MFormGridContext MFormGridContext { get; set; }


        [Parameter]
        public bool EnableValidation { get; set; } = true;

        [Parameter]
        public RenderFragment Fields { get; set; }

        [Parameter]
        public EventCallback<MFormSubmitArgs> OnValidSubmit { get; set; }

        [Parameter]
        public EventCallback<MFormValueChangedArgs<T>> OnValueChanged { get; set; }

        [Inject]
        public IStringLocalizer<MComponentsLocalization> L { get; set; }


        protected HashSet<FieldIdentifier> ChangedValues { get; set; } = new HashSet<FieldIdentifier>();


        public List<IMField> FieldList = new List<IMField>();


        protected override void OnInitialized()
        {
            base.OnInitialized();

            mEditContext = new EditContext(Model);
            mEditContext.OnFieldChanged += _fixedEditContext_OnFieldChanged;

            if (ContainerContext != null)
            {
                ContainerContext.RegisterForm(this);
                ContainerContext.OnFormSubmit += CascadedFormContext_OnFormSubmit;
            }
        }
             
        protected override void OnParametersSet()
        {
            base.OnParametersSet();
        }

        private void _fixedEditContext_OnFieldChanged(object sender, FieldChangedEventArgs e)
        {
            ChangedValues.Add(e.FieldIdentifier);
        }

        private void NotifyContainer()
        {
            ContainerContext?.NotifySubmit(L);
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            base.BuildRenderTree(builder);

            // If _fixedEditContext changes, tear down and recreate all descendants.
            // This is so we can safely use the IsFixed optimization on Cascadi ngValue,
            // optimizing for the common case where _fixedEditContext never changes.
            builder.OpenRegion(mEditContext.GetHashCode());

            if (!IsInTableRow)
            {
                builder.OpenElement(0, "form");
                builder.AddMultipleAttributes(1, AdditionalAttributes);
                builder.AddAttribute(2, "onsubmit", EventCallback.Factory.Create(this, NotifyContainer));

                if (EnableValidation)
                    builder.AddAttribute(11, "class", "m-form-validation");
            }


            if (Fields != null)
            {
                RenderFragment child3() =>
                        (builder2) =>
                        {
                            builder2.AddMarkupContent(1, "\r\n");
                            builder2.AddContent(2, this.Fields);
                            builder2.AddMarkupContent(3, "\r\n");
                        };

                builder.OpenComponent<CascadingValue<MForm<T>>>(4);
                builder.AddAttribute(5, "Value", this);
                builder.AddAttribute(6, "ChildContent", child3());
                builder.CloseComponent();
            }

            RenderFragment child(EditContext context) =>
                    (builder2) =>
                    {
                        if (EnableValidation)
                        {
                            builder2.OpenComponent<DataAnnotationsValidator>(0);
                            builder2.CloseComponent();
                        }

                        if (!IsInTableRow)
                        {
                            builder2.OpenComponent<ValidationSummary>(1);
                            builder2.CloseComponent();
                        }

                        if (FieldList.Any())
                        {
                            foreach (var groupResult in GroupByRow(FieldList))
                            {
                                Process(builder2, groupResult);
                            }
                        }
                        else
                        {
                            foreach (var groupResult in GroupByRow(ReflectionHelper.GetProperties(Model).Select(pi => GetField(pi))))
                            {
                                //    Console.WriteLine(property.PropertyType.FullName);

                                Process(builder2, groupResult);
                            }
                        }

                        if (!IsInTableRow)
                            builder2.AddMarkupContent(27, "<button type=\"submit\" style=\"display: none;\">Submit</button>\r\n");
                    };

            builder.OpenComponent<CascadingValue<EditContext>>(3);
            builder.AddAttribute(4, "IsFixed", true);
            builder.AddAttribute(5, "Value", mEditContext);
            builder.AddAttribute(6, "ChildContent", child(mEditContext));
            builder.CloseComponent();

            if (!IsInTableRow)
            {
                builder.CloseElement();
            }

            builder.CloseRegion();
        }

        private IEnumerable<IGrouping<int, IMField>> GroupByRow(IEnumerable<IMField> pFields)
        {
            return pFields.GroupBy(p =>
            {
                var rowAttr = p.Attributes?.FirstOrDefault(a => a.GetType() == typeof(RowAttribute)) as RowAttribute;

                if (rowAttr == null)
                    return 0;

                return rowAttr.RowId;
            }).OrderByDescending(g => g.Key).Reverse();
        }

        protected IMPropertyInfo GetPropertyInfo(IMPropertyField pField)
        {
            if (pField.Property == null && pField.PropertyType == null)
            {
                return new MEmptyPropertyInfo();
            }

            var pi = ReflectionHelper.GetIMPropertyInfo(Model.GetType(), pField.Property, pField.PropertyType);

            if (pi.PropertyType == null)
            {
                pi.PropertyType = pField.PropertyType ?? throw new InvalidOperationException($"Could not find type for {pField.Property}. Please specify it");
            }

            if (pi is MPropertyExpandoInfo ei)
            {
                ei.Attributes = pField.Attributes;
            }

            return pi;
        }

        protected IMPropertyField GetField(IMPropertyInfo pPropertyInfo)
        {
            return new MField()
            {
#pragma warning disable BL0005 // Component parameter should not be set outside of its component.

                Attributes = pPropertyInfo.GetAttributes()?.ToArray(),
                Property = pPropertyInfo.Name,
                PropertyType = pPropertyInfo.PropertyType

#pragma warning restore BL0005 // Component parameter should not be set outside of its component.
            };
        }

        protected void Process(RenderTreeBuilder builder2, IGrouping<int, IMField> groupResult)
        {
            if (groupResult.Key != 0 && !IsInTableRow)
            {
                builder2.OpenElement(10, "div");
                builder2.AddAttribute(11, "class", "m-form-row" + (groupResult.Count() > 1 ? " multiple-forms-in-row" : string.Empty));
            }

            foreach (var field in groupResult)
            {
                if (field is IMPropertyField propField)
                {
                    var propertyInfo = GetPropertyInfo(propField);

                    if (propertyInfo.GetCustomAttribute(typeof(HiddenAttribute)) != null)
                        continue;

                    if (field.Attributes != null)
                        propertyInfo.SetAttributes(field.Attributes);

                    var inpId = Guid.NewGuid();

                    if (IsInTableRow)
                    {
                        builder2.OpenElement(16, "td");
                        //       builder2.AddMultipleAttributes(17, field.AdditionalAttributes);
                        // update 13.07.2020, add AdditionalAttributes to Input

                        if (field.AdditionalAttributes != null && field.AdditionalAttributes.TryGetValue(Extensions.MFORM_IN_TABLE_ROW_TD_STYLE_ATTRIBUTE, out object value))
                        {
                            builder2.AddAttribute(247, "style", value);
                        }

                        if (propertyInfo.PropertyType != null)
                        {
                            AddInput(builder2, field, propertyInfo, inpId);
                        }

                        builder2.CloseElement();
                        continue;
                    }

                    if (groupResult.Key == 0)
                    {
                        builder2.OpenElement(10, "div");
                        builder2.AddAttribute(11, "class", "m-form-row");
                    }

                    //  <div class="form-group">
                    builder2.OpenElement(10, "div");

                    string cssClass = "form-group col-"; //TODO we use bootstrap here - good idea or bad?

                    if (groupResult.Key == 0)
                    {
                        cssClass += "12";
                    }
                    else
                    {
                        cssClass += 12 / groupResult.Count();
                    }

                    builder2.AddAttribute(11, "class", cssClass);

                    //  <label for="@inpId">@property.Name</label>
                    builder2.OpenElement(13, "label");
                    builder2.AddAttribute(14, "for", inpId);
                    builder2.AddAttribute(14, "class", "col-sm-12 col-form-label"); //TODO we use bootstrap here - good idea or bad?

                    var displayAttribute = propertyInfo.GetCustomAttribute(typeof(DisplayAttribute)) as DisplayAttribute;
                    if (displayAttribute != null)
                    {
                        builder2.AddContent(280, displayAttribute.Name);
                    }
                    else
                    {
                        builder2.AddContent(284, propertyInfo.Name);
                    }

                    builder2.CloseElement();

                    builder2.OpenElement(16, "div");
                    builder2.AddAttribute(17, "class", "col-sm-12");  //TODO we use bootstrap here - good idea or bad?

                    AddInput(builder2, field, propertyInfo, inpId);

                    builder2.CloseElement(); // </div>


                    builder2.CloseElement(); // </div>

                    if (groupResult.Key == 0)
                    {
                        builder2.CloseElement(); // </div>
                    }
                }
                else if (field is IMFieldGenerator fieldGenerator)
                {
                    MFieldGeneratorContext context = new MFieldGeneratorContext()
                    {
                        Form = this
                    };

                    if (IsInTableRow)
                    {
                        builder2.OpenElement(16, "td");
                    }

                    builder2.AddContent(42, fieldGenerator.Template?.Invoke(context));

                    if (IsInTableRow)
                    {
                        builder2.CloseElement();
                    }
                }
            }

            if (groupResult.Key != 0 && !IsInTableRow)
            {
                builder2.CloseElement();
            }
        }

        private void AddInput(RenderTreeBuilder builder2, IMField field, IMPropertyInfo propertyInfo, Guid inpId)
        {
            if (field is IMPropertyField pf)
            {
                if (field is IMComplexField)
                {
                    var appendMethod = typeof(RenderHelper).GetMethod(nameof(RenderHelper.AppendComplexType)).MakeGenericMethod(typeof(T), pf.PropertyType);
                    appendMethod.Invoke(null, new object[] { builder2, propertyInfo, Model, inpId, this, field, MFormGridContext });
                    return;
                }

                bool isInFilterRow = AdditionalAttributes != null && AdditionalAttributes.ContainsKey("data-is-filterrow");

                var method = typeof(RenderHelper).GetMethod(nameof(RenderHelper.AppendInput)).MakeGenericMethod(propertyInfo.PropertyType);
                method.Invoke(null, new object[] { builder2, propertyInfo, Model, inpId, this, isInFilterRow, field });
            }
        }

        public bool HasUnsavedChanges
        {
            get { return ChangedValues.Count > 0; }
        }

        private void CascadedFormContext_OnFormSubmit(object sender, MFormContainerContextSubmitArgs e)
        {
            // Console.WriteLine("FormContextSubmit: " + typeof(T));

            var isValid = mEditContext.Validate(); // This will likely become ValidateAsync later

            if (!isValid)
            {
                if (ContainerContext != null)
                {
                    throw new UserMessageException("Please check the values. There is at least one validation error!");
                }
                Console.WriteLine(typeof(T) + ": Not valid!");
                return;
            }

            Dictionary<string, object> changedDict = new Dictionary<string, object>();

            if (HasUnsavedChanges)
            {
                foreach (var entry in ChangedValues)
                {
                    string fieldname = entry.FieldName;

                    if (changedDict.ContainsKey(fieldname))
                        continue;

                    //01.08.2020 entry.Model is an old wrong version of the model in client side
                    object value = ReflectionHelper.GetPropertyValue(Model, entry.FieldName);
                    changedDict.Add(fieldname, value);
                }

                ChangedValues.Clear();
            }

            if (OnValidSubmit.HasDelegate)
            {
                var task = OnValidSubmit.InvokeAsync(new MFormSubmitArgs(mEditContext, changedDict, Model, e.UserInterated));
                task.Wait();
            }
        }

        public void CallLocalSubmit(bool pUserInteracted)
        {
            var args = new MFormContainerContextSubmitArgs()
            {
                UserInterated = pUserInteracted
            };

            CascadedFormContext_OnFormSubmit(null, args);
        }

        public void RegisterField(IMField pField)
        {
            FieldList.Add(pField);
            StateHasChanged();
        }

        public void UnregisterField(IMField pField)
        {
            FieldList.Remove(pField);
            StateHasChanged();
        }

        public void ClearFields()
        {
            FieldList.Clear();
            StateHasChanged();
        }

        public void OnInputKeyUp(KeyboardEventArgs pArgs)
        {
            if (pArgs.Key == "Enter")
            {
                if (ContainerContext == null)
                {
                    CallLocalSubmit(true);
                }
            }
        }

        public void OnInputValueChanged(string pProperty, object pNewValue)
        {
            if (OnValueChanged.HasDelegate)
            {
                var task = OnValueChanged.InvokeAsync(new MFormValueChangedArgs<T>(pProperty, pNewValue, Model));
                task.Wait();
            }
        }
    }
}
