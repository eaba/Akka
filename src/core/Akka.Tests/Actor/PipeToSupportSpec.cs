﻿//-----------------------------------------------------------------------
// <copyright file="PipeToSupportSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.TestKit;
using FluentAssertions;
using Xunit;

namespace Akka.Tests.Actor
{
    public class PipeToSupportSpec : AkkaSpec
    {
        private readonly TaskCompletionSource<string> _taskCompletionSource;
        private readonly Task<string> _task;
        private readonly Task _taskWithoutResult;

        public PipeToSupportSpec()
        {
            _taskCompletionSource = new TaskCompletionSource<string>();
            _task = _taskCompletionSource.Task;
            _taskWithoutResult = _taskCompletionSource.Task;
            Sys.EventStream.Subscribe(TestActor, typeof(DeadLetter));
        }
        
        [Fact]
        public void Should_immediately_PipeTo_completed_Task()
        {
            var task = Task.FromResult("foo");
            task.PipeTo(TestActor);
            ExpectMsg("foo");
        }

        [Fact]
        public void Should_by_default_send_task_result_as_message()
        {
            _task.PipeTo(TestActor);
            _taskCompletionSource.SetResult("Hello");
            ExpectMsg("Hello");
        }

        [Fact]
        public void Should_by_default_not_send_a_success_message_if_the_task_does_not_produce_a_result()
        {
            _taskWithoutResult.PipeTo(TestActor);
            _taskCompletionSource.SetResult("Hello");
            ExpectNoMsg(TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void Should_by_default_send_task_exception_as_status_failure_message()
        {
            _task.PipeTo(TestActor);
            _taskWithoutResult.PipeTo(TestActor);
            _taskCompletionSource.SetException(new Exception("Boom"));
            ExpectMsg<Status.Failure>(x => x.Cause.Message == "Boom");
            ExpectMsg<Status.Failure>(x => x.Cause.Message == "Boom");
        }

        [Fact]
        public void Should_use_success_handling_to_transform_task_result()
        {
            _task.PipeTo(TestActor, success: x => "Hello " + x);
            _taskWithoutResult.PipeTo(TestActor, success: () => "Hello");
            _taskCompletionSource.SetResult("World");
            var pipeTo = ReceiveN(2).Cast<string>().ToList();
            pipeTo.Should().Contain("Hello");
            pipeTo.Should().Contain("Hello World");
        }

        [Fact]
        public void Should_use_failure_handling_to_transform_task_exception()
        {
            _task.PipeTo(TestActor, failure: e => "Such a " + e.Message);
            _taskWithoutResult.PipeTo(TestActor, failure: e => "Such a " + e.Message);
            _taskCompletionSource.SetException(new Exception("failure..."));
            ExpectMsg("Such a failure...");
            ExpectMsg("Such a failure...");
        }
    }
}
