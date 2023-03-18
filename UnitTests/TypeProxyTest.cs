using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DotNetify.Blazor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace UnitTests
{
   [TestClass]
   public class TypeProxyTest
   {
      public interface IState
      {
         string StringValue { get; set; }
         int IntValue { get; set; }
         double DoubleValue { get; set; }
         IEnumerable<string> StringEnumerable { get; set; }
         IEnumerable<int> IntEnumerable { get; set; }
      }

      public interface IStateWithWatch
      {
         [Watch]
         public string StringValue { get; set; }
      }
      
      public interface IStateDisposable
      {
         public void Dispose();
         public Task DisposeAsync();
      }

      public interface IStateWithMethods
      {
         public void Submit();

         public void SubmitString(string arg);

         public void SubmitInt(int arg);

         public void SubmitDouble(double arg);

         public void SubmitList(List<string> arg);

         public void SubmitDynamic(dynamic arg);
      }
      
      public interface IStateWithAsyncMethods
      {
         public Task Submit();

         public Task SubmitString(string arg);

         public Task SubmitInt(int arg);

         public Task SubmitDouble(double arg);

         public Task SubmitList(List<string> arg);

         public Task SubmitDynamic(dynamic arg);
      }

      [TestMethod]
      public void TypeProxy_CanSetDefaultValues()
      {
         var obj = TypeProxy.Create<IState>();
         
         Assert.AreEqual(null, obj.StringValue);
         Assert.AreEqual(default(int), obj.IntValue);
         Assert.AreEqual(default(double), obj.DoubleValue);
         Assert.AreEqual(null, obj.StringEnumerable);
         Assert.AreEqual(null, obj.IntEnumerable);
      }

      [TestMethod]
      public void TypeProxy_CanCreateObject()
      {
         var obj = TypeProxy.Create<IState>();
         
         obj.StringValue = "hello";
         obj.IntValue = int.MaxValue;
         obj.DoubleValue = Math.PI;
         obj.StringEnumerable = new List<string> { "Alpha", "Omega" };
         obj.IntEnumerable = new int[] { int.MinValue, int.MaxValue };

         Assert.AreEqual("hello", obj.StringValue);
         Assert.AreEqual(int.MaxValue, obj.IntValue);
         Assert.AreEqual(Math.PI, obj.DoubleValue);
         Assert.AreEqual("Alpha", obj.StringEnumerable.First());
         Assert.AreEqual("Omega", obj.StringEnumerable.Last());
         Assert.AreEqual(int.MinValue, obj.IntEnumerable.First());
         Assert.AreEqual(int.MaxValue, obj.IntEnumerable.Last());
      }

      [TestMethod]
      public void TypeProxy_CanSerializeObject()
      {
         var obj = TypeProxy.Create<IState>();

         obj.StringValue = "hello";
         obj.IntValue = int.MaxValue;
         obj.DoubleValue = Math.PI;
         obj.StringEnumerable = new List<string> { "Alpha", "Omega" };
         obj.IntEnumerable = new int[] { int.MinValue, int.MaxValue };

         string serialized = JsonSerializer.Serialize(obj);
         Assert.AreEqual("{\"StringValue\":\"hello\",\"IntValue\":2147483647,\"DoubleValue\":3.141592653589793,\"StringEnumerable\":[\"Alpha\",\"Omega\"],\"IntEnumerable\":[-2147483648,2147483647]}", serialized);
      }

      [TestMethod]
      public void TypeProxy_CanDeserializeObject()
      {
         string data = "{\"StringValue\":\"hello\",\"IntValue\":2147483647,\"DoubleValue\":3.141592653589793,\"StringEnumerable\":[\"Alpha\",\"Omega\"],\"IntEnumerable\":[-2147483648,2147483647]}";

         IState obj = TypeProxy.Deserialize<IState>(data);
      
         Assert.AreEqual("hello", obj.StringValue);
         Assert.AreEqual(int.MaxValue, obj.IntValue);
         Assert.AreEqual(Math.PI, obj.DoubleValue);
         Assert.AreEqual("Alpha", obj.StringEnumerable.First());
         Assert.AreEqual("Omega", obj.StringEnumerable.Last());
         Assert.AreEqual(int.MinValue, obj.IntEnumerable.First());
         Assert.AreEqual(int.MaxValue, obj.IntEnumerable.Last());
      }

      [TestMethod]
      public void TypeProxy_CanWatchObject()
      {
         var obj = TypeProxy.Create<IStateWithWatch>();
         string watched = null;

         var mockVMProxy = Substitute.For<IVMProxy>();
         mockVMProxy.DispatchAsync(Arg.Any<string>(), Arg.Any<object>()).Returns(args =>
         {
            watched = nameof(IStateWithWatch.StringValue);
            return Task.CompletedTask;
         });
         (obj as IVMState).VMProxy = mockVMProxy;

         obj.StringValue = "hello";
         Assert.AreEqual(nameof(IStateWithWatch.StringValue), watched);
      }

      [TestMethod]
      public void TypeProxy_CanCreateObjectMethod()
      {
         var obj = TypeProxy.Create<IStateWithMethods>();
         string name = null;
         object value = null;

         var mockVMProxy = Substitute.For<IVMProxy>();
         mockVMProxy.DispatchAsync(Arg.Any<string>(), Arg.Any<object>()).Returns(args =>
         {
            name = args[0].ToString();
            value = args[1];
            return Task.CompletedTask;
         });
         (obj as IVMState).VMProxy = mockVMProxy;

         obj.Submit();
         Assert.AreEqual(nameof(IStateWithMethods.Submit), name);
         Assert.IsNull(value);

         var stringValue = "hello world";
         obj.SubmitString(stringValue);
         Assert.AreEqual(nameof(IStateWithMethods.SubmitString), name);
         Assert.AreEqual(stringValue, value);

         var intValue = int.MaxValue;
         obj.SubmitInt(intValue);
         Assert.AreEqual(nameof(IStateWithMethods.SubmitInt), name);
         Assert.AreEqual(intValue, value);

         var doubleValue = Math.PI;
         obj.SubmitDouble(doubleValue);
         Assert.AreEqual(nameof(IStateWithMethods.SubmitDouble), name);
         Assert.AreEqual(doubleValue, value);

         var listValue = new List<string> { "Hello", "World" };
         obj.SubmitList(listValue);
         Assert.AreEqual(nameof(IStateWithMethods.SubmitList), name);
         Assert.AreEqual(listValue, value);

         var dynamicValue = new { FirstName = "Hello", LastName = "World" };
         obj.SubmitDynamic(dynamicValue);
         Assert.AreEqual(nameof(IStateWithMethods.SubmitDynamic), name);
         Assert.AreEqual(dynamicValue, value);
      }
      
      [TestMethod]
      public async Task TypeProxy_CanCreateObjectAsyncMethod()
      {
         var obj = TypeProxy.Create<IStateWithAsyncMethods>();
         string name = null;
         object value = null;

         var mockVMProxy = Substitute.For<IVMProxy>();
         mockVMProxy.DispatchAsync(Arg.Any<string>(), Arg.Any<object>()).Returns(args =>
         {
            name = args[0].ToString();
            value = args[1];
            return Task.CompletedTask;
         });
         (obj as IVMState).VMProxy = mockVMProxy;

         await obj.Submit();
         Assert.AreEqual(nameof(IStateWithMethods.Submit), name);
         Assert.IsNull(value);

         var stringValue = "hello world";
         await obj.SubmitString(stringValue);
         Assert.AreEqual(nameof(IStateWithMethods.SubmitString), name);
         Assert.AreEqual(stringValue, value);

         var intValue = int.MaxValue;
         await obj.SubmitInt(intValue);
         Assert.AreEqual(nameof(IStateWithMethods.SubmitInt), name);
         Assert.AreEqual(intValue, value);

         var doubleValue = Math.PI;
         await obj.SubmitDouble(doubleValue);
         Assert.AreEqual(nameof(IStateWithMethods.SubmitDouble), name);
         Assert.AreEqual(doubleValue, value);

         var listValue = new List<string> { "Hello", "World" };
         await obj.SubmitList(listValue);
         Assert.AreEqual(nameof(IStateWithMethods.SubmitList), name);
         Assert.AreEqual(listValue, value);

         var dynamicValue = new { FirstName = "Hello", LastName = "World" };
         await obj.SubmitDynamic(dynamicValue);
         Assert.AreEqual(nameof(IStateWithMethods.SubmitDynamic), name);
         Assert.AreEqual(dynamicValue, value);
      }
      
      [TestMethod]
      public void TypeProxy_CanCreateObjectDisposable()
      {
         var obj = TypeProxy.Create<IStateDisposable>();
         var mockVMProxy = Substitute.For<IVMProxy>();
         (obj as IVMState).VMProxy = mockVMProxy;
         
         obj.Dispose();

         mockVMProxy.Received().DisposeAsync();
      }
      
      [TestMethod]
      public async Task TypeProxy_CanCreateObjectAsyncDisposable()
      {
         var obj = TypeProxy.Create<IStateDisposable>();
         var mockVMProxy = Substitute.For<IVMProxy>();
         (obj as IVMState).VMProxy = mockVMProxy;
         
         await obj.DisposeAsync();

         await mockVMProxy.Received().DisposeAsync();
      }
   }
}