/*
Copyright 2020 Dicky Suryadi

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Castle.DynamicProxy;


[assembly: InternalsVisibleTo("DotNetify.Blazor.UnitTests")]

namespace DotNetify.Blazor
{
   public class TypeProxyException : Exception
   {
      public TypeProxyException(string message) : base(message)
      {
      }
   }

   public abstract class BaseObject : IVMState
   {
      [JsonIgnore]
      public IVMProxy VMProxy { get; set; }
   }

   internal class ProxyTypeInfoResolver : DefaultJsonTypeInfoResolver
   {
      public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
      {
         JsonTypeInfo typeInfo = base.GetTypeInfo(type, options);

         if (typeInfo.Kind == JsonTypeInfoKind.Object && typeInfo.Type.IsInterface)
         {
            typeInfo.CreateObject = () => TypeProxy.Create(type);
         }

         return typeInfo;
      }
   }

   internal class ProxyInterceptor : IInterceptor
   {
      private readonly Dictionary<string, object> _propValues = new Dictionary<string, object>();
      private readonly PropertyInfo[] _interfaceProperties;
      
      private enum ReservedMethod
      {
         Dispatch,
         Dispose,
         DispatchAsync,
         DisposeAsync
      }

      public ProxyInterceptor(Type interfaceType)
      {
         _interfaceProperties = interfaceType.GetProperties();
      }

      public void Intercept(IInvocation invocation)
      {
         if (IsProperty(invocation))
         {
            HandleProperty(invocation);
         }
         else
         {
            HandleMethod(invocation);
         }
      }
      
      private bool IsProperty(IInvocation invocation)
      {
         var method = invocation.Method;
         return _interfaceProperties.Any(prop => prop.GetAccessors().Contains(method));
      }
      
      private bool IsAsyncMethod(IInvocation invocation)
      {
         var method = invocation.Method;
         var returnType = method.ReturnType;
         return typeof(Task).IsAssignableFrom(returnType);
      }
      
      private void HandleProperty(IInvocation invocation)
      {
         var method = invocation.Method;
         var property = _interfaceProperties.First(prop => prop.GetAccessors().Contains(method));
        
         if (invocation.Method == property.SetMethod)
         {
            HandleSetProperty(invocation, property);
         }
         else
         {
            HandleGetProperty(invocation, property);
         }
      }

      private void HandleSetProperty(IInvocation invocation, PropertyInfo property)
      {
         var value = invocation.Arguments.First();
         _propValues[property.Name] = value;

         var hasWatchAttribute = property.GetCustomAttribute<WatchAttribute>() != null;
         if (hasWatchAttribute)
         {
            var vmProxy = GetVMProxy(invocation);
            vmProxy.DispatchAsync(property.Name, value);
         }
      }

      private void HandleGetProperty(IInvocation invocation, PropertyInfo property)
      {
         if (!_propValues.ContainsKey(property.Name))
         {
            invocation.ReturnValue = GetDefaultPropertyValue(property);
         }
         else
         {
            invocation.ReturnValue = _propValues[property.Name];
         }
      }

      private void HandleMethod(IInvocation invocation)
      {
         var vmProxy = GetVMProxy(invocation);
         var methodName = invocation.Method.Name;
         Task returnTask;
         
         if (methodName == nameof(ReservedMethod.Dispose) || methodName == nameof(ReservedMethod.DisposeAsync))
         {
            returnTask = vmProxy.DisposeAsync();
         }
         else if (methodName == nameof(ReservedMethod.Dispatch) || methodName == nameof(ReservedMethod.DispatchAsync))
         {
            var value = invocation.Arguments.FirstOrDefault();
            if (value is Dictionary<string, object> properties)
            {
               returnTask = vmProxy.DispatchAsync(properties);
            }
            else
            {
               throw new TypeProxyException("'Dispatch' is a reserved method that requires a single argument of type Dictionary<string, object>.");
            }
         }
         else
         {
            var value = invocation.Arguments.FirstOrDefault();
            returnTask = vmProxy.DispatchAsync(methodName, value);
         }

         if (IsAsyncMethod(invocation))
         {
            invocation.ReturnValue = returnTask;
         }
      }

      private IVMProxy GetVMProxy(IInvocation invocation)
      {
         var vmProxy = ((BaseObject)invocation.Proxy).VMProxy;

         if (vmProxy == null)
         {
            throw new TypeProxyException("VMProxy has not been provided");
         }

         return vmProxy;
      }
      
      private object GetDefaultPropertyValue(PropertyInfo propertyInfo)
      {
         return GetType()
                .GetMethod(nameof(GetDefaultGeneric), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(propertyInfo.PropertyType)
                .Invoke(this, null);
      }

      private TValue GetDefaultGeneric<TValue>()
      {
         return default;
      }
   }

   public static class TypeProxy
   {
      private static readonly ProxyGenerator Generator = new ProxyGenerator();
      
      public static T Create<T>()
      {
         return (T)Create(typeof(T));
      }
      
      public static object Create(Type type)
      {
         var proxy = Generator.CreateInterfaceProxyWithoutTarget(type, new ProxyGenerationOptions()
         {
            BaseTypeForInterfaceProxy = typeof(BaseObject)
         }, new ProxyInterceptor(type));
         return proxy;
      }

      public static T Deserialize<T>(string data)
      {
         var jsonSettings = new JsonSerializerOptions()
         {
            TypeInfoResolver = new ProxyTypeInfoResolver()
         };
         
         return JsonSerializer.Deserialize<T>(data, jsonSettings);
      }
   }
   
}