using UnityEngine;
using KBEngine;
using System.Collections;
using System;
using System.Xml;
using System.Collections.Generic;

public class KBEEventProc
{
	public static KBEEventProc inst = null;
	public static bool enterSpace = false;
	public static List<Int32> pendingEnterEntityIDs = new List<Int32>();
	
	public KBEEventProc()
	{
		KBEEventProc.inst = this;
		KBEngine.Event.registerOut("addSpaceGeometryMapping", KBEEventProc.inst, "addSpaceGeometryMapping");
		
		KBEngine.Event.registerOut("onAvatarEnterWorld", KBEEventProc.inst, "onAvatarEnterWorld");
		KBEngine.Event.registerOut("onEnterWorld", KBEEventProc.inst, "onEnterWorld");
		KBEngine.Event.registerOut("onLeaveWorld", KBEEventProc.inst, "onLeaveWorld");
		
		KBEngine.Event.registerOut("set_HP", KBEEventProc.inst, "set_HP");
		KBEngine.Event.registerOut("set_MP", KBEEventProc.inst, "set_MP");
		KBEngine.Event.registerOut("set_HP_Max", KBEEventProc.inst, "set_HP_Max");
		KBEngine.Event.registerOut("set_MP_Max", KBEEventProc.inst, "set_MP_Max");
		KBEngine.Event.registerOut("set_level", KBEEventProc.inst, "set_level");
		KBEngine.Event.registerOut("set_name", KBEEventProc.inst, "set_name");
		KBEngine.Event.registerOut("set_state", KBEEventProc.inst, "set_state");
		KBEngine.Event.registerOut("set_moveSpeed", KBEEventProc.inst, "set_moveSpeed");
		KBEngine.Event.registerOut("set_modelScale", KBEEventProc.inst, "set_modelScale");
		KBEngine.Event.registerOut("set_modelID", KBEEventProc.inst, "set_modelID");
		KBEngine.Event.registerOut("set_position", KBEEventProc.inst, "set_position");
		KBEngine.Event.registerOut("set_direction", KBEEventProc.inst, "set_direction");
		KBEngine.Event.registerOut("updatePosition", KBEEventProc.inst, "updatePosition");
		KBEngine.Event.registerOut("recvDamage", KBEEventProc.inst, "recvDamage");
		KBEngine.Event.registerOut("otherAvatarOnJump", KBEEventProc.inst, "otherAvatarOnJump");
	}
	
	public void addSpaceGeometryMapping(string respath)
	{
		// 这个事件可以理解为服务器通知客户端加载指定的场景资源
		// 通过服务器的api KBEngine.addSpaceGeometryMapping设置到spaceData中，进入space的玩家就会被同步spaceData里面的内容
		string[] sArray = respath.Split('/');
		respath = sArray[sArray.Length - 1];
		loader.inst.enterScene(respath);
		enterSpace = true;
		
		foreach(Int32 id in pendingEnterEntityIDs)
		{
			KBEngine.Entity entity = KBEngineApp.app.findEntity(id);
			if(entity != null)
				onEnterWorld(entity);
		}
		
		pendingEnterEntityIDs.Clear();
	}
	
	public void onAvatarEnterWorld(UInt64 rndUUID, Int32 eid, KBEngine.Avatar avatar)
	{
		onEnterWorld(avatar);
	}
	
	public void onEnterWorld(KBEngine.Entity entity)
	{
		if(enterSpace == false)
		{
			pendingEnterEntityIDs.Add(entity.id);
			return;
		}
		
		int modelID = 0;
		string name = "";
		int hp = -1, hpmax = -1;
		object state = null;
		object modelScale = null;
		object moveSpeed = null;

		// 底层使用了插件生成技术， 此处临时这么获得。
		if(entity.className == "Avatar")
		{
			modelID = (int)((KBEngine.Avatar)entity).modelID;
			modelScale = ((KBEngine.Avatar)entity).modelScale;
			name = ((KBEngine.Avatar)entity).name;
			hp = (int)((KBEngine.Avatar)entity).HP;
			hpmax = (int)((KBEngine.Avatar)entity).HP_Max;
			state = ((KBEngine.Avatar)entity).state;
			moveSpeed = ((KBEngine.Avatar)entity).moveSpeed;
			
		}
		else if(entity.className == "Monster")
		{
			modelID = (int)((KBEngine.Monster)entity).modelID;
			modelScale = ((KBEngine.Monster)entity).modelScale;
			name = ((KBEngine.Monster)entity).name;
			hp = (int)((KBEngine.Monster)entity).HP;
			hpmax = (int)((KBEngine.Monster)entity).HP_Max;
			state = ((KBEngine.Monster)entity).state;
			moveSpeed = ((KBEngine.Monster)entity).moveSpeed;
		}
		else if(entity.className == "NPC")
		{
			modelID = (int)((KBEngine.NPC)entity).modelID;
			modelScale = ((KBEngine.NPC)entity).modelScale;
			name = ((KBEngine.NPC)entity).name;
			moveSpeed = ((KBEngine.NPC)entity).moveSpeed;
		}
		else if(entity.className == "Gate")
		{
			modelID = (int)((KBEngine.Gate)entity).modelID;
			modelScale = ((KBEngine.Gate)entity).modelScale;
			name = ((KBEngine.Gate)entity).name;
		}

		Asset newasset = Scene.findAsset(modelID + ".unity3d", true, "");
		newasset.createAtScene = loader.inst.currentSceneName;
		
		SceneEntityObject obj = new SceneEntityObject();
		obj.kbentity = entity;
		
		if(entity.isPlayer())
			obj.createPlayer();
		else
			obj.create();
		
		entity.renderObj = obj;
		
		Scene scene = null;
		if(!loader.inst.scenes.TryGetValue(loader.inst.currentSceneName, out scene))
		{
			Common.ERROR_MSG("KBEEventProc::onEnterWorld: not found scene(" + loader.inst.currentSceneName + ")!");
			return;
		}
		
		newasset.loadLevel = Asset.LOAD_LEVEL.LEVEL_IDLE;
		obj.asset = newasset;
		obj.idkey = "_entity_" + entity.id;
		
		obj.position = entity.position;
		obj.eulerAngles = new Vector3(entity.direction.y, entity.direction.z, entity.direction.x);
		obj.destDirection = obj.eulerAngles;

		if(name != "")
			obj.setName((string)name);
		
		if(hp != -1)
			obj.updateHPBar((Int32)hp, (Int32)hpmax);
		
		if(state != null)
			set_state(entity, state);
		
		if(modelScale != null)
			set_modelScale(entity, modelScale);
		
		if(moveSpeed != null)
		{
			set_moveSpeed(entity, moveSpeed);
		}
		
		if(entity.className == "Avatar")
			newasset.unloadLevel = Asset.UNLOAD_LEVEL.LEVEL_FORBID;
		
		newasset.refs.Add(obj.idkey);
		scene.addSceneObject(obj.idkey, obj);

		if(newasset.isLoaded || newasset.bundle != null)
		{
			obj.Instantiate();
			newasset.refs.Remove(obj.idkey);
		}
		else
		{
			loader.inst.loadPool.addLoad(newasset);
			loader.inst.loadPool.start();
		}
	}
	
	public void onLeaveWorld(KBEngine.Entity entity)
	{
		if(enterSpace == false)
		{
			pendingEnterEntityIDs.Remove(entity.id);
			return;
		}
		
		Scene scene = null;
		if(!loader.inst.scenes.TryGetValue(loader.inst.currentSceneName, out scene))
		{
			Common.ERROR_MSG("KBEEventProc::onLeaveWorld: not found scene(" + loader.inst.currentSceneName + ")!");
			return;
		}
		
		scene.removeSceneObject("_entity_" + entity.id); 
		((SceneEntityObject)entity.renderObj).kbentity = null;
		entity.renderObj = null;
	}
	
	public void set_position(KBEngine.Entity entity)
	{
		if(entity.renderObj != null)
		{
			Common.calcPositionY(entity.position, out entity.position.y, false);
			((SceneObject)entity.renderObj).position = entity.position;
			((SceneEntityObject)entity.renderObj).destPosition = entity.position;
		}
	
		if(entity.isPlayer() == false)
			return;
			
		RPG_Controller.initPos.x = entity.position.x;
		RPG_Controller.initPos.y = entity.position.y;
		RPG_Controller.initPos.z = entity.position.z;

		if(RPG_Controller.instance != null)
		{
			RPG_Controller.instance.transform.position = RPG_Controller.initPos;
			Common.DEBUG_MSG("KBEEventProc::set_position: entity.position=" + entity.position + " " + entity.position + ", RPG_Controller.position=" + RPG_Controller.instance.transform.position);
		}
	}

	public void updatePosition(KBEngine.Entity entity)
	{
		if(enterSpace == false)
			return;
		
		if(entity.renderObj != null)
		{
			((SceneEntityObject)entity.renderObj).updatePosition(entity.position);
		}
	}
	
	public void set_direction(KBEngine.Entity entity)
	{
		if(enterSpace == false)
			return;
		
		if(entity.isPlayer() == false)
		{
			if(entity.renderObj != null)
			{
				((SceneEntityObject)entity.renderObj).destDirection = new Vector3(entity.direction.y, entity.direction.z, entity.direction.x);
				//((SceneObject)entity.renderObj).rotation = new Vector3(entity.direction.y, entity.direction.z, entity.direction.x);
			}
			return;
		}
	
		RPG_Controller.initRot = new Vector3(entity.direction.y, entity.direction.z, entity.direction.x);
		if(RPG_Controller.instance != null)
		{
			RPG_Controller.instance.transform.Rotate(RPG_Controller.rotation);
			Common.DEBUG_MSG("KBEEventProc::set_direction: RPG_Controller.rotation=" + RPG_Controller.instance.transform.rotation);
		}
	}

	public void set_HP(KBEngine.Entity entity, object v, object hpmax)
	{
		object vv = null;

		// 底层使用了插件生成技术， 此处临时这么获得。
		if(entity.className == "Avatar")
		{
			vv = (int)((KBEngine.Avatar)entity).HP_Max;

			if(entity.renderObj != null)
			{
				object oldvv = ((KBEngine.Avatar)entity).getTempProperty("old_HP");
				if(oldvv != null)
				{
					Int32 diff = (Int32)vv - (Int32)oldvv;
				
					if(diff != 0)
					{
						((SceneEntityObject)entity.renderObj).addHP(diff);
					}
					
					((KBEngine.Avatar)entity).setTempProperty("old_HP", vv);
				}
				else
					((KBEngine.Avatar)entity).setTempProperty("old_HP", vv);
			}
			
		}
		else if(entity.className == "Monster")
		{
			vv = (int)((KBEngine.Monster)entity).HP_Max;

			if(entity.renderObj != null)
			{
				object oldvv = ((KBEngine.Monster)entity).getTempProperty("old_HP");
				if(oldvv != null)
				{
					Int32 diff = (Int32)vv - (Int32)oldvv;
				
					if(diff != 0)
					{
						((SceneEntityObject)entity.renderObj).addHP(diff);
					}
					
					((KBEngine.Monster)entity).setTempProperty("old_HP", vv);
				}
				else
					((KBEngine.Monster)entity).setTempProperty("old_HP", vv);
			}
		}
		
		if(entity.isPlayer())
		{
			game_ui_autopos.updatePlayerBar_HP(v, vv);
		}
		else
		{
			game_ui_autopos.updateTargetBar_HP(v, vv);
		}
	}
	
	public void set_MP(KBEngine.Entity entity, object v, object mpmax)
	{
		object vv = null;

		// 底层使用了插件生成技术， 此处临时这么获得。
		if(entity.className == "Avatar")
		{
			vv = (int)((KBEngine.Avatar)entity).MP_Max;

			if(entity.renderObj != null)
			{
				object oldvv = ((KBEngine.Avatar)entity).getTempProperty("old_MP");
				if(oldvv != null)
				{
					Int32 diff = (Int32)vv - (Int32)oldvv;
				
					if(diff != 0)
					{
						((SceneEntityObject)entity.renderObj).addMP(diff);
					}
					
					((KBEngine.Avatar)entity).setTempProperty("old_MP", vv);
				}
				else
					((KBEngine.Avatar)entity).setTempProperty("old_MP", vv);
			}
			
		}
		else if(entity.className == "Monster")
		{
			vv = (int)((KBEngine.Monster)entity).HP_Max;

			if(entity.renderObj != null)
			{
				object oldvv = ((KBEngine.Monster)entity).getTempProperty("old_MP");
				if(oldvv != null)
				{
					Int32 diff = (Int32)vv - (Int32)oldvv;
				
					if(diff != 0)
					{
						((SceneEntityObject)entity.renderObj).addMP(diff);
					}
					
					((KBEngine.Monster)entity).setTempProperty("old_MP", vv);
				}
				else
					((KBEngine.Monster)entity).setTempProperty("old_MP", vv);
			}
		}
		
		if(entity.isPlayer())
		{
			game_ui_autopos.updatePlayerBar_MP(v, vv);
		}
		else
		{
			game_ui_autopos.updateTargetBar_MP(v, vv);
		}
	}
	
	public void set_HP_Max(KBEngine.Entity entity, object v, object hp)
	{
		object vv = null; 
		if(entity.className == "Avatar")
		{
			vv = ((KBEngine.Avatar)entity).HP;
		}
		else if(entity.className == "Monster")
		{
			vv = ((KBEngine.Monster)entity).HP;
		}

		if(entity.isPlayer())
		{
			game_ui_autopos.updatePlayerBar_HP(vv, v);
		}
		else
		{
			game_ui_autopos.updateTargetBar_HP(vv, v);
		}
	}
	
	public void set_MP_Max(KBEngine.Entity entity, object v, object mp)
	{
		object vv = null; 
		if(entity.className == "Avatar")
		{
			vv = ((KBEngine.Avatar)entity).MP;
		}
		else if(entity.className == "Monster")
		{
			vv = ((KBEngine.Monster)entity).MP;
		}

		if(entity.isPlayer())
		{
			game_ui_autopos.updatePlayerBar_MP(vv, v);
		}
		else
		{
			game_ui_autopos.updateTargetBar_MP(vv, v);
		}
	}
	
	public void set_level(KBEngine.Entity entity, object v)
	{
		if(game_ui_autopos.level_label == null)
			return;
		
		if(entity.isPlayer())
		{
			game_ui_autopos.level_label.text = "lv: " + v;
		}
		else
		{
			game_ui_autopos.target_level_label.text = "lv:" + v;
		}
	}
	
	public void set_name(KBEngine.Entity entity, object v)
	{
		if(entity.isPlayer())
		{
			if(game_ui_autopos.name_label == null)
				return;
			
			game_ui_autopos.name_label.text = (string)v;
		}
		else
		{
			if(entity.renderObj != null)
			{
				((SceneEntityObject)entity.renderObj).setName((string)v);
			}
		}
	}
	
	public void set_state(KBEngine.Entity entity, object v)
	{
		if(enterSpace == false)
			return;
		
		if(entity.renderObj != null)
		{
			((SceneEntityObject)entity.renderObj).set_state((SByte)v);
		}
		
		if(entity.isPlayer())
		{
			RPG_Controller.enabled = ((SByte)v) != 1;
			
			if(RPG_Controller.enabled == false)
				game_ui_autopos.showRelivePanel();
			else
				game_ui_autopos.hideRelivePanel();
		}
	}

	public void set_moveSpeed(KBEngine.Entity entity, object v)
	{
		if(enterSpace == false)
			return;
		
		float fspeed = ((float)(Byte)v) / 10f;
		
		if(entity.isPlayer())
		{
			if(RPG_Controller.instance != null)
				RPG_Controller.instance.walkSpeed = fspeed;
		}
		
		if(entity.renderObj != null)
		{
			((SceneEntityObject)entity.renderObj).set_moveSpeed(fspeed);
		}
	}
	
	public void set_modelScale(KBEngine.Entity entity, object v)
	{
		if(enterSpace == false)
			return;

		if(entity.renderObj != null)
		{
			float scale = (((float)(Byte)v) / 10.0f);
			((SceneEntityObject)entity.renderObj).scale = new Vector3(scale, scale, scale);
		}
	}
	
	public void set_modelID(KBEngine.Entity entity, object v)
	{
		if(enterSpace == false)
			return;
	}
	
	public void recvDamage(KBEngine.Entity entity, KBEngine.Entity attacker, Int32 skillID, Int32 damageType, Int32 damage)
	{
		if(enterSpace == false)
			return;

		if(attacker != null)
		{
			if(attacker.renderObj != null)
			{
				((SceneEntityObject)attacker.renderObj).attack(skillID, damageType, ((SceneEntityObject)entity.renderObj));
			}
		}

		if(entity.renderObj != null)
		{
			if(CameraTargeting.inst != null && CameraTargeting.inst.lasttargetTransform == null && entity.isPlayer())
			{
				CameraTargeting.inst.setTarget(((SceneEntityObject)attacker.renderObj).gameEntity.transform);
				TargetManger.setTarget((SceneEntityObject)attacker.renderObj);
			}
			
			((SceneEntityObject)entity.renderObj).recvDamage(skillID, damageType, damage);
		}
	}
	
	public void otherAvatarOnJump(KBEngine.Entity entity)
	{
		if(entity.renderObj != null)
		{
			SceneEntityObject seo = ((SceneEntityObject)entity.renderObj);
			seo.stopPlayAnimation("");
			seo.playJumpAnimation();
		}
	}
}

