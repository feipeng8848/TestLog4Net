protected void SetParameter(XmlElement element, object target) 
{
    // Get the property name
    string name = element.GetAttribute(NAME_ATTR); //NAME_ATTR = "name"

    // If the name attribute does not exist then use the name of the element
    if (element.LocalName != PARAM_TAG || name == null || name.Length == 0)
    {
        name = element.LocalName;
    }

    // Look for the property on the target object
    Type targetType = target.GetType();
    Type propertyType = null;

    PropertyInfo propInfo = null;
    MethodInfo methInfo = null;

    // Try to find a writable property
    propInfo = targetType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
    if (propInfo != null && propInfo.CanWrite)
    {
        // found a property
        propertyType = propInfo.PropertyType;
    }
    else
    {
        propInfo = null;

        // look for a method with the signature Add<property>(type)
        methInfo = FindMethodInfo(targetType, name);

        if (methInfo != null)
        {
            propertyType = methInfo.GetParameters()[0].ParameterType;
        }
    }

    if (propertyType == null)
    {
        LogLog.Error(declaringType, "XmlHierarchyConfigurator: Cannot find Property [" + name + "] to set object on [" + target.ToString() + "]");
    }
    else
    {
        string propertyValue = null;

        if (element.GetAttributeNode(VALUE_ATTR) != null)
        {
            propertyValue = element.GetAttribute(VALUE_ATTR);
        }
        else if (element.HasChildNodes)
        {
            // Concatenate the CDATA and Text nodes together
            foreach(XmlNode childNode in element.ChildNodes)
            {
                if (childNode.NodeType == XmlNodeType.CDATA || childNode.NodeType == XmlNodeType.Text)
                {
                    if (propertyValue == null)
                    {
                        propertyValue = childNode.InnerText;
                    }
                    else
                    {
                        propertyValue += childNode.InnerText;
                    }
                }
            }
        }

        if(propertyValue != null)
        {
#if !(NETCF || NETSTANDARD1_3) // NETSTANDARD1_3: System.Runtime.InteropServices.RuntimeInformation not available on desktop 4.6
            try
            {
                // Expand environment variables in the string.
                IDictionary environmentVariables = Environment.GetEnvironmentVariables();
                if (HasCaseInsensitiveEnvironment) {
                environmentVariables = CreateCaseInsensitiveWrapper(environmentVariables);
                }
                propertyValue = OptionConverter.SubstituteVariables(propertyValue, environmentVariables);
            }
            catch(System.Security.SecurityException)
            {
                // This security exception will occur if the caller does not have 
                // unrestricted environment permission. If this occurs the expansion 
                // will be skipped with the following warning message.
                LogLog.Debug(declaringType, "Security exception while trying to expand environment variables. Error Ignored. No Expansion.");
            }
#endif

            Type parsedObjectConversionTargetType = null;

            // Check if a specific subtype is specified on the element using the 'type' attribute
            string subTypeString = element.GetAttribute(TYPE_ATTR);
            if (subTypeString != null && subTypeString.Length > 0)
            {
                // Read the explicit subtype
                try
                {
#if NETSTANDARD1_3
                    Type subType = SystemInfo.GetTypeFromString(this.GetType().GetTypeInfo().Assembly, subTypeString, true, true);
#else
                    Type subType = SystemInfo.GetTypeFromString(subTypeString, true, true);
#endif

                    LogLog.Debug(declaringType, "Parameter ["+name+"] specified subtype ["+subType.FullName+"]");

                    if (!propertyType.IsAssignableFrom(subType))
                    {
                        // Check if there is an appropriate type converter
                        if (OptionConverter.CanConvertTypeTo(subType, propertyType))
                        {
                            // Must re-convert to the real property type
                            parsedObjectConversionTargetType = propertyType;

                            // Use sub type as intermediary type
                            propertyType = subType;
                        }
                        else
                        {
                            LogLog.Error(declaringType, "subtype ["+subType.FullName+"] set on ["+name+"] is not a subclass of property type ["+propertyType.FullName+"] and there are no acceptable type conversions.");
                        }
                    }
                    else
                    {
                        // The subtype specified is found and is actually a subtype of the property
                        // type, therefore we can switch to using this type.
                        propertyType = subType;
                    }
                }
                catch(Exception ex)
                {
                    LogLog.Error(declaringType, "Failed to find type ["+subTypeString+"] set on ["+name+"]", ex);
                }
            }

            // Now try to convert the string value to an acceptable type
            // to pass to this property.

            object convertedValue = ConvertStringTo(propertyType, propertyValue);
            
            // Check if we need to do an additional conversion
            if (convertedValue != null && parsedObjectConversionTargetType != null)
            {
                LogLog.Debug(declaringType, "Performing additional conversion of value from [" + convertedValue.GetType().Name + "] to [" + parsedObjectConversionTargetType.Name + "]");
                convertedValue = OptionConverter.ConvertTypeTo(convertedValue, parsedObjectConversionTargetType);
            }

            if (convertedValue != null)
            {
                if (propInfo != null)
                {
                    // Got a converted result
                    LogLog.Debug(declaringType, "Setting Property [" + propInfo.Name + "] to " + convertedValue.GetType().Name + " value [" + convertedValue.ToString() + "]");

                    try
                    {
                        // Pass to the property
#if NETSTANDARD1_3 // TODO BindingFlags is available for netstandard1.5
                        propInfo.SetValue(target, convertedValue, null);
#else
                        propInfo.SetValue(target, convertedValue, BindingFlags.SetProperty, null, null, CultureInfo.InvariantCulture);
#endif
                    }
                    catch(TargetInvocationException targetInvocationEx)
                    {
                        LogLog.Error(declaringType, "Failed to set parameter [" + propInfo.Name + "] on object [" + target + "] using value [" + convertedValue + "]", targetInvocationEx.InnerException);
                    }
                }
                else if (methInfo != null)
                {
                    // Got a converted result
                    LogLog.Debug(declaringType, "Setting Collection Property [" + methInfo.Name + "] to " + convertedValue.GetType().Name + " value [" + convertedValue.ToString() + "]");

                    try
                    {
                        // Pass to the property
#if NETSTANDARD1_3 // TODO BindingFlags is available for netstandard1.5
                        methInfo.Invoke(target, new[] { convertedValue });
#else
                        methInfo.Invoke(target, BindingFlags.InvokeMethod, null, new object[] {convertedValue}, CultureInfo.InvariantCulture);
#endif
                    }
                    catch(TargetInvocationException targetInvocationEx)
                    {
                        LogLog.Error(declaringType, "Failed to set parameter [" + name + "] on object [" + target + "] using value [" + convertedValue + "]", targetInvocationEx.InnerException);
                    }
                }
            }
            else
            {
                LogLog.Warn(declaringType, "Unable to set property [" + name + "] on object [" + target + "] using value [" + propertyValue + "] (with acceptable conversion types)");
            }
        }
        else
        {
            object createdObject = null;

            if (propertyType == typeof(string) && !HasAttributesOrElements(element))
            {
                // If the property is a string and the element is empty (no attributes
                // or child elements) then we special case the object value to an empty string.
                // This is necessary because while the String is a class it does not have
                // a default constructor that creates an empty string, which is the behavior
                // we are trying to simulate and would be expected from CreateObjectFromXml
                createdObject = "";
            }
            else
            {
                // No value specified
                Type defaultObjectType = null;
                if (IsTypeConstructible(propertyType))
                {
                    defaultObjectType = propertyType;
                }

                createdObject = CreateObjectFromXml(element, defaultObjectType, propertyType);
            }

            if (createdObject == null)
            {
                LogLog.Error(declaringType, "Failed to create object to set param: "+name);
            }
            else
            {
                if (propInfo != null)
                {
                    // Got a converted result
                    LogLog.Debug(declaringType, "Setting Property ["+ propInfo.Name +"] to object ["+ createdObject +"]");

                    try
                    {
                        // Pass to the property
#if NETSTANDARD1_3 // TODO BindingFlags is available for netstandard1.5
                        propInfo.SetValue(target, createdObject, null);
#else
                        propInfo.SetValue(target, createdObject, BindingFlags.SetProperty, null, null, CultureInfo.InvariantCulture);
#endif
                    }
                    catch(TargetInvocationException targetInvocationEx)
                    {
                        LogLog.Error(declaringType, "Failed to set parameter [" + propInfo.Name + "] on object [" + target + "] using value [" + createdObject + "]", targetInvocationEx.InnerException);
                    }
                }
                else if (methInfo != null)
                {
                    // Got a converted result
                    LogLog.Debug(declaringType, "Setting Collection Property ["+ methInfo.Name +"] to object ["+ createdObject +"]");

                    try
                    {
                        // Pass to the property
#if NETSTANDARD1_3 // TODO BindingFlags is available for netstandard1.5
                        methInfo.Invoke(target, new[] { createdObject });
#else
                        methInfo.Invoke(target, BindingFlags.InvokeMethod, null, new object[] {createdObject}, CultureInfo.InvariantCulture);
#endif
                    }
                    catch(TargetInvocationException targetInvocationEx)
                    {
                        LogLog.Error(declaringType, "Failed to set parameter [" + methInfo.Name + "] on object [" + target + "] using value [" + createdObject + "]", targetInvocationEx.InnerException);
                    }
                }
            }
        }
    }
}