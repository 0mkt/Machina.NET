﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BRobot
{
    //   ██████╗██╗   ██╗██████╗ ███████╗ ██████╗ ██████╗ 
    //  ██╔════╝██║   ██║██╔══██╗██╔════╝██╔═══██╗██╔══██╗
    //  ██║     ██║   ██║██████╔╝███████╗██║   ██║██████╔╝
    //  ██║     ██║   ██║██╔══██╗╚════██║██║   ██║██╔══██╗
    //  ╚██████╗╚██████╔╝██║  ██║███████║╚██████╔╝██║  ██║
    //   ╚═════╝ ╚═════╝ ╚═╝  ╚═╝╚══════╝ ╚═════╝ ╚═╝  ╚═╝
    //                                                    
    /// <summary>
    /// Represents an abstraction of the state of a robotic device. 
    /// Keeps track of things such as position, orientation, joint configuration,
    /// current speed, zone, etc.
    /// Useful as virtual representation of a simulated or controller robot actuator. 
    /// </summary>
    abstract class RobotCursor
    {
        // Public props
        public string name;
        public Point position;
        public Rotation rotation;
        public Joints joints;
        public int speed;
        public int zone;
        public MotionType motionType;
        protected bool initialized = false;
        private bool applyImmediately = false;  // when an action is issued to this cursor, apply it immediately?

        /// <summary>
        /// Manages pending and released Actions, plus blocks. 
        /// </summary>
        public ActionBuffer buffer;

        /// <summary>
        /// Specified RobotCursor instance will be issued all Actions 
        /// released from this one. 
        /// </summary>
        public RobotCursor child;

        /// <summary>
        /// Robot program compilers now belong to the RobotCursor. 
        /// It makes it easier to attach the right device-specific type, 
        /// and to use the cursor's information to generate the program. 
        /// </summary>
        public Compiler compiler;

        /// <summary>
        /// A lock for buffer manipulation operations. 
        /// </summary>
        public object bufferLock = new object();


        public RobotCursor(string name, bool applyImmediately)
        {
            this.name = name;
            this.applyImmediately = applyImmediately;

            buffer = new ActionBuffer();
        }


        // Abstract methods
        public abstract bool ApplyAction(ActionTranslation action);
        public abstract bool ApplyAction(ActionRotation action);
        public abstract bool ApplyAction(ActionTranslationAndRotation action);
        public abstract bool ApplyAction(ActionRotationAndTranslation action);
        public abstract bool ApplyAction(ActionJoints action);
        public abstract bool ApplyAction(ActionMessage action);
        public abstract bool ApplyAction(ActionWait action);


        /// <summary>
        /// Minimum information necessary to initialize a robot object.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="rot"></param>
        public bool Initialize(Point pos, Rotation rot)
        {
            position = new Point(pos);
            rotation = new Rotation(rot);
            speed = -1;
            zone = -1;
            motionType = MotionType.Undefined;

            initialized = true;
            return initialized;
        }

        public void SetChild(RobotCursor childCursor)
        {
            child = childCursor;
        }
        
        /// <summary>
        /// Add an action to this cursor's buffer, to be released whenever assigned priority.
        /// </summary>
        /// <param name="action"></param>
        public bool Issue(Action action)
        {
            lock(bufferLock)
            {
                buffer.Add(action);
                if (applyImmediately)
                {
                    return ApplyNextAction();
                }
                return true;
            }
            
        }

        /// <summary>
        /// Applies next single action pending in the buffer.
        /// </summary>
        /// <returns></returns>
        public bool ApplyNextAction()
        {
            lock(bufferLock)
            {
                Action next = buffer.GetNext();
                if (next == null) return false;
                bool success = ApplyAction(next);
                if (success)
                {
                    child.Issue(next);
                }
                return success;
            }
        }

        /// <summary>
        /// Requests all un-blocked pending Actions in the buffer to be flagged
        /// as a block. 
        /// </summary>
        public void QueueActions()
        {
            buffer.SetBlock();
        }

        /// <summary>
        /// Are there Actions pending in the buffer?
        /// </summary>
        /// <returns></returns>
        public bool AreActionsPending()
        {
            return buffer.AreActionsPending();
        }

        /// <summary>
        /// Applies the directives of an Action to this Cursor.
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public bool ApplyAction(Action action)
        {
            // @TOPAN: is there a better way to choose the subclass?
            //return ApplyAction((action.GetType())action);

            // This will need a lot of maintenance, add new cases for each actiontype. 
            // Better way to do it?
            switch (action.type)
            {
                case ActionType.Translation:
                    return ApplyAction((ActionTranslation)action);

                case ActionType.Rotation:
                    return ApplyAction((ActionRotation)action);

                case ActionType.TranslationAndRotation:
                    return ApplyAction((ActionTranslationAndRotation)action);

                case ActionType.RotationAndTranslation:
                    return ApplyAction((ActionRotationAndTranslation)action);

                case ActionType.Joints:
                    return ApplyAction((ActionJoints)action);

                case ActionType.Message:
                    return ApplyAction((ActionMessage)action);

                case ActionType.Wait:
                    return ApplyAction((ActionWait)action);

            }

            Console.WriteLine("Could not apply action " + action);
            return false;
        }

        /// <summary>
        /// Return a device-specific program with all the Actions pending in the buffer.
        /// </summary>
        /// <returns></returns>
        public List<string> ProgramFromBuffer()
        {
            return compiler.UNSAFEProgramFromBuffer("BRobotProgram", this, false);
        }

        /// <summary>
        /// Return a device-specific program with the next block of Actions pending in the buffer.
        /// </summary>
        /// <returns></returns>
        public List<string> ProgramFromBlock()
        {
            return compiler.UNSAFEProgramFromBuffer("BRobotProgram", this, true);
        }

        



    }




    //   █████╗ ██████╗ ██████╗ 
    //  ██╔══██╗██╔══██╗██╔══██╗
    //  ███████║██████╔╝██████╔╝
    //  ██╔══██║██╔══██╗██╔══██╗
    //  ██║  ██║██████╔╝██████╔╝
    //  ╚═╝  ╚═╝╚═════╝ ╚═════╝ 
    //                          
    internal class RobotCursorABB : RobotCursor
    {
        
        public RobotCursorABB(string name, bool applyImmediately) : base(name, applyImmediately)
        {
            compiler = new CompilerABB();
        }

        public override bool ApplyAction(ActionTranslation action)
        {
            Point newPosition = new Point();

            if (action.relativeTranslation)
            {
                // If user issued a relative action, make sure there are absolute values to work with. (This limitation is due to current lack of FK/IK solvers)
                if (position == null || rotation == null)
                {
                    Console.WriteLine("Sorry, must provide absolute position values first before applying relative ones..." + this);
                    return false;
                }

                if (action.worldTranslation)
                {
                    newPosition = position + action.translation;
                }
                else
                {
                    Point worldVector = Point.Rotation(action.translation, Rotation.Conjugate(this.rotation));
                    newPosition = position + worldVector;
                }
            }
            else
            {
                // Fail if issued abs movement without prior rotation info. (This limitation is due to current lack of FK/IK solvers)
                if (rotation == null)
                {
                    Console.WriteLine("Sorry, currently missing TCP orientation to work with..." + this);
                    return false;
                }

                newPosition.Set(action.translation);
            }

            // @TODO: this must be more programmatically implemented 
            if (Control.SAFETY_CHECK_TABLE_COLLISION)
            {
                if (Control.IsBelowTable(newPosition.Z))
                {
                    if (Control.SAFETY_STOP_ON_TABLE_COLLISION)
                    {
                        Console.WriteLine("Cannot perform action: too close to base XY plane --> TCP.z = {0}", newPosition.Z);
                        return false;
                    }
                    else
                    {
                        Console.WriteLine("WARNING: too close to base XY plane, USE CAUTION! --> TCP.z = {0}", newPosition.Z);
                    }
                }
            }

            position = newPosition;
            joints = null;      // flag joints as null to avoid Joint instructions using obsolete data

            // If valid inputs, update, otherwise stick with previous values
            if (action.speed != -1) speed = action.speed;
            if (action.zone != -1) zone = action.zone;
            if (action.motionType != MotionType.Undefined) motionType = action.motionType;

            return true;
        }

        //public override bool RevertAction(ActionTranslation action)
        //{

        //}


        public override bool ApplyAction(ActionRotation action)
        {
            // @TODO: implement some kind of security check here...
            if (action.relativeRotation)
            {
                // If user issued a relative action, make sure there are absolute values to work with. (This limitation is due to current lack of FK/IK solvers)
                if (position == null || rotation == null)
                {
                    Console.WriteLine("Sorry, must provide absolute rotation values first before applying relative ones..." + this);
                    return false;
                }

                if (action.worldRotation)
                {
                    rotation.PreMultiply(action.rotation);
                }
                else
                {
                    rotation.Multiply(action.rotation);
                }
            }
            else
            {
                // Fail if issued abs rotation without prior position info. (This limitation is due to current lack of FK/IK solvers)
                if (position == null)
                {
                    Console.WriteLine("Sorry, currently missing TCP position to work with..." + this);
                    return false;
                }

                rotation = new Rotation(action.rotation);
            }

            joints = null;      // flag joints as null to avoid Joint instructions using obsolete data

            // If valid inputs, update, otherwise stick with previous values
            if (action.speed != -1) speed = action.speed;
            if (action.zone != -1) zone = action.zone;
            if (action.motionType != MotionType.Undefined) motionType = action.motionType;

            return true;
        }


        public override bool ApplyAction(ActionTranslationAndRotation action)
        {
            Point newPos = new Point();

            if (action.relativeTranslation)
            {
                // If user issued a relative action, make sure there are absolute values to work with. (This limitation is due to current lack of FK/IK solvers)
                if (position == null || rotation == null)
                {
                    Console.WriteLine("Sorry, must provide absolute transform values first before applying relative ones..." + this);
                    return false;
                }

                if (action.worldTranslation)
                {
                    newPos = position + action.translation;
                }
                else
                {
                    Point worldVector = Point.Rotation(action.translation, Rotation.Conjugate(this.rotation));
                    newPos = position + worldVector;
                }
            }
            else
            {
                newPos.Set(action.translation);
            }

            // @TODO: this must be more programmatically implemented 
            if (Control.SAFETY_CHECK_TABLE_COLLISION)
            {
                if (Control.IsBelowTable(newPos.Z))
                {
                    if (Control.SAFETY_STOP_ON_TABLE_COLLISION)
                    {
                        Console.WriteLine("Cannot perform action: too close to base XY plane --> TCP.z = {0}", newPos.Z);
                        return false;
                    }
                    else
                    {
                        Console.WriteLine("WARNING: too close to base XY plane, USE CAUTION! --> TCP.z = {0}", newPos.Z);
                    }
                }
            }

            // @TODO: implement some kind of security check here...
            if (action.relativeRotation)
            {
                // If user issued a relative action, make sure there are absolute values to work with. (This limitation is due to current lack of FK/IK solvers)
                if (position == null || rotation == null)
                {
                    Console.WriteLine("Sorry, must provide absolute transform values first before applying relative ones..." + this);
                    return false;
                }

                if (action.worldRotation)
                {
                    rotation.PreMultiply(action.rotation);
                }
                else
                {
                    rotation.Multiply(action.rotation);
                }
            }
            else
            {
                //rotation.Set(action.rotation);
                rotation = new Rotation(action.rotation);
            }

            position = newPos;
            joints = null;  // flag joints as null to avoid Joint instructions using obsolete data

            // If valid inputs, update, otherwise stick with previous values
            if (action.speed != -1) speed = action.speed;
            if (action.zone != -1) zone = action.zone;
            if (action.motionType != MotionType.Undefined) motionType = action.motionType;

            return true;
        }


        public override bool ApplyAction(ActionRotationAndTranslation action)
        {
            // @TODO: implement some kind of security check here...
            Rotation newRot;
            if (action.relativeRotation)
            {
                // If user issued a relative action, make sure there are absolute values to work with. (This limitation is due to current lack of FK/IK solvers)
                if (position == null || rotation == null)
                {
                    Console.WriteLine("Sorry, must provide absolute transform values first before applying relative ones..." + this);
                    return false;
                }

                if (action.worldRotation)
                {
                    //rotation.PreMultiply(action.rotation);
                    newRot = Rotation.Multiply(action.rotation, rotation);
                }
                else
                {
                    //rotation.Multiply(action.rotation);
                    newRot = Rotation.Multiply(rotation, action.rotation);
                }
            }
            else
            {
                //newRot.Set(action.rotation);
                newRot = new Rotation(action.rotation);
            }

            Point newPos;
            if (action.relativeTranslation)
            {
                // If user issued a relative action, make sure there are absolute values to work with. (This limitation is due to current lack of FK/IK solvers)
                if (position == null || rotation == null)
                {
                    Console.WriteLine("Sorry, must provide absolute transform values first before applying relative ones..." + this);
                    return false;
                }

                if (action.worldTranslation)
                {
                    newPos = position + action.translation;
                }
                else
                {
                    Point worldVector = Point.Rotation(action.translation, Rotation.Conjugate(newRot));
                    newPos = position + worldVector;
                }
            }
            else
            {
                //newPos.Set(action.translation);
                newPos = new Point(action.translation);
            }

            // @TODO: this must be more programmatically implemented 
            if (Control.SAFETY_CHECK_TABLE_COLLISION)
            {
                if (Control.IsBelowTable(newPos.Z))
                {
                    if (Control.SAFETY_STOP_ON_TABLE_COLLISION)
                    {
                        Console.WriteLine("Cannot perform action: too close to base XY plane --> TCP.z = {0}", newPos.Z);
                        return false;
                    }
                    else
                    {
                        Console.WriteLine("WARNING: too close to base XY plane, USE CAUTION! --> TCP.z = {0}", newPos.Z);
                    }
                }
            }

            rotation = newRot;
            position = newPos;
            joints = null;  // flag joints as null to avoid Joint instructions using obsolete data

            // If valid inputs, update, otherwise stick with previous values
            if (action.speed != -1) speed = action.speed;
            if (action.zone != -1) zone = action.zone;
            if (action.motionType != MotionType.Undefined) motionType = action.motionType;

            return true;
        }


        public override bool ApplyAction(ActionJoints action)
        {
            
            // @TODO: implement joint limits checks and general safety...

            // Modify current Joints
            if (action.relativeJoints)
            {
                // If user issued a relative action, make sure there are absolute values to work with. 
                // (This limitation is due to current lack of FK/IK solvers)
                if (joints == null)  // could also check for motionType == MotionType.Joints
                {
                    Console.WriteLine("Sorry, must provide absolute Joints values first before applying relative ones..." + this);
                    return false;
                }
                joints.Add(action.joints);
            }
            else
            {
                joints = new Joints(action.joints);  // create a new object since it was probably null
            }

            // Flag the lack of other geometric data
            position = null;
            rotation = null;

            // If valid inputs, update, otherwise stick with previous values
            if (action.speed != -1) speed = action.speed;
            if (action.zone != -1) zone = action.zone;
            if (action.motionType != MotionType.Undefined) motionType = action.motionType;

            return true;
        }

        
        public override bool ApplyAction(ActionMessage action)
        {
            // There is basically nothing to do here! Leave the state of the robot as-is.
            // Maybe do some Console output?
            return true;
        }

        public override bool ApplyAction(ActionWait action)
        {
            // There is basically nothing to do here! Leave the state of the robot as-is.
            return true;
        }








        /// <summary>
        /// Returns an ABB robtarget representation of the current state of the cursor.
        /// WARNING: this method is EXTREMELY UNSAFE; it performs no IK calculations, assigns default [0,0,0,0] 
        /// robot configuration and assumes the robot controller will figure out the correct one.
        /// </summary>
        /// <returns></returns>
        public string GetUNSAFERobTargetDeclaration()
        {
            return string.Format("[{0},{1},[0,0,0,0],[0,9E9,9E9,9E9,9E9,9E9]]", position, rotation);
        }

        public string GetJointTargetDeclaration()
        {
            return string.Format("[{0},[0,9E9,9E9,9E9,9E9,9E9]]", joints);
        }
        



        public override string ToString()
        {
            return string.Format("{5}: {0} p{1} r{2} j{6} v{3} z{4}", motionType, position, rotation, speed, zone, name, joints);
        }

    }
}
