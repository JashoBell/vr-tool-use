# Experiment Instruction Generation

# Follow directions at https://codelabs.developers.google.com/codelabs/cloud-text-speech-python3/ to connect to Text-to-speech API
# You will paste this into IPython once you have it running.
# This function is adapted from page 9: https://codelabs.developers.google.com/codelabs/cloud-text-speech-python3/#8
# Function takes a voice name (see: https://cloud.google.com/text-to-speech/docs/voices, Neural2 and Wavenet are the highest quality), 
# text you want to turn into .wav files (each sentence being a new file), and a label that will be appended to the name of the eventual files.
# filename becomes "voice_name_label_#.wav", with # indicating order in the sequence of saved .wav files
# 

import google.cloud.texttospeech as tts
import os

def text_to_wav(voice_name: str, speech_text: str, label: str, in_sentences: bool = True, path: str = None):
    language_code = "-".join(voice_name.split("-")[:2])
    text_input = tts.SynthesisInput(text=speech_text)
    voice_params = tts.VoiceSelectionParams(
        language_code=language_code, name=voice_name
    )
    audio_config = tts.AudioConfig(audio_encoding=tts.AudioEncoding.LINEAR16)

    client = tts.TextToSpeechClient()
    if path:
        # Create path if it does not exist, then change to it
        if not os.path.exists(path):
            os.mkdir(path)
        # If current path is already the path, do nothing
        if os.getcwd() != path:
            os.chdir(path)
        
    if in_sentences:
        speech_parts = speech_text.split(".")
        for i, speech_part in enumerate(speech_parts):
            if speech_part.strip() != "":
                text_input.text = speech_part
                response = client.synthesize_speech(
                    input=text_input, voice=voice_params, audio_config=audio_config
                )
                filename = f"{voice_name}_{label}_{i+1}.wav"
                with open(filename, "wb") as out:
                    out.write(response.audio_content)
                    print(f'Generated speech saved to "{filename}"')
    else:
        response = client.synthesize_speech(
            input=text_input, voice=voice_params, audio_config=audio_config
        )
        filename = f"{voice_name}_{label}.wav"
        with open(filename, "wb") as out:
            out.write(response.audio_content)
            print(f'Generated speech saved to "{filename}"')


# Below is some of the code that was used to generate the audio files for the experiment. There may be some slight differences for instances where I tweaked the text slightly in the cloud console and forgot to update this script.

# Calibration Audio
## Welcome & Eye Tracker
text_to_wav("en-US-Wavenet-C", "Welcome to “Characterizing Human Movements in Real and Virtual Environments”! This part of the study is meant to give you a moment to acclimate to the environment. You will notice that it is a close replica of the real environment. Everything you see is physically present. First, you will go through an eye tracking calibration procedure. It will first have you adjust the faceplate of the headset so that your eyes are centered within it. It will then have you adjust the lenses using the knob on the right side of the very front of the headset. Finally, it will ask you to follow a dot using your eyes. Stand still, and the procedure will begin.", "eye_tracker_calibration")
text_to_wav("en-US-Wavenet-C", "Against the wall is a projector screen that will display helpful diagrams during this portion of the study.", "wrist_calibration_pt2")
## Avatar Calibration
text_to_wav("en-US-Wavenet-C", "Looks like everything went well! Now, you will be given a virtual body that is calibrated to your real body. Please stand up straight with your arms at your side.", "avatar_calibration_1")
text_to_wav("en-US-Wavenet-C", "Great! Now put your arms out straight at your sides as if you are the letter T, and hold this pose for a couple of seconds.", "avatar_calibration_2")
text_to_wav("en-US-Wavenet-C", "Nice. Now, please look at your hands and feet, and let the researcher know if anything seems out of place. If everything seems okay, walk into the green space that you see in the corner of the room.", "avatar_calibration_3")

## Walking Task
text_to_wav("en-US-Wavenet-C", "Next, you should find where the green space moved in the room and walk into it. Once you enter it, the space will move across the room. Continue walking into the green spaces until you hear that the task is ending. Feel free to take whatever path you would like, but please avoid stepping through the area marked in red. That is the source of this universe, and is also very expensive.", "walking_task")

## End of Calibration
text_to_wav("en-US-Wavenet-C", "Great! That ends this part of the study. Please make your way to the seat next to the table and ease yourself in, making sure to feel around while taking a seat. Please do not move the chair when you take a seat.", "end_calibration_1")
text_to_wav("en-US-Wavenet-C", "The researcher will now remove the trackers from your ankles. To your left, you will see a controller on the table, please grab the controller and hold it in your left hand at the edge of the table. Please make sure to maintain good posture throughout the study, with your back straight and against the back of the chair. If you need to readjust the chair's position, the researcher will assist you. If you are ready to begin, click the controller's large circular touchpad with your thumb.",  "end_calibration_2", False)

# Main Experiment Audio
## Opening
text_to_wav("en-US-Wavenet-C", "The main part of the study is starting. From this point on, you will be asked to complete a sequence of different tasks. Each task will be preceded by a short instructional video. After the videos, you will be able to continue to the task, or replay the video, using the controller in your left hand. You may also ask the researcher questions if the video was not clear. After each task, there will be a short break while the researcher readies the next task.", "opening_instructions", False)

# Landmark Task Audio
text_to_wav("en-US-Wavenet-C", "Your body is now hidden from view. For the upcoming task, please place your right forearm on the table in the green highlighted area, moving forward in the chair if you need to. The researcher will adjust it to prepare for the task. Once they finish, please do not move your arm until it reappears after the task. The task will not start if you move your arm. Using your left hand, hold the controller with your thumb on the trackpad and index finger on the trigger. In the upcoming task, you will be trying to estimate the location of landmarks on your hidden right arm as it rests on the table. The three possible targets for this task include your wrist, elbow, and the middle of your forearm, exactly halfway between the wrist and elbow.  Each trial will start with a three second countdown.  The target for the trial will be visible on a floating panel above the table during the countdown. When the countdown finishes, the target will be verbally called out and a floating sphere, with the same width as a quarter, will appear moving along the length of your hidden arm. Your goal on each trial is to stop the sphere as close to the target as possible. To stop the sphere, press the large circular trackpad under your thumb. Once you make your response, the sphere will disappear, and the next trial’s countdown will begin. There is no time constraint on responding. Please take as long as you need to ensure an accurate response. At the beginning of the task, the table will become larger, and its surface will become black. On every new trial, the sphere will appear in a different location, move at a different speed, and \"turn around\" at different points in space. Please keep in mind that the goal for this task is to assess your perception, not assess your ability to strategically optimize your responses using an explicit strategy. This task will last for 18 trials.", "landmark_task")
text_to_wav("en-US-Wavenet-C", "Elbow. Wrist. Forearm.", "landmark_task_targets")
text_to_wav("en-US-Wavenet-C", "When the countdown finishes, the target will be verbally called out. A floating sphere with the same width as a quarter will then appear, and begin moving along the length of your hidden arm.", "landmark_task")

# Reaching Task Audio
text_to_wav("en-US-Wavenet-C", "In the upcoming task, the goal is to use your right hand to accurately grasp, lift and replace the object in front of you on consecutive trials. You should begin with the tips of your thumb and index finger pinched together within the small green cube at the edge of the table to your right. Each trial will begin with a three second countdown, with the final tone signaling that you should begin. If you move your fingers out of the starting area before the countdown ends, it will restart. When the trial begins, reach out and pick up the object from the sides using a precision grip. Aim for the centers of the left and right faces of the object, and only use your thumb and index finger to grasp it. Once you have grasped the object, lift it from the table to touch the green area before replacing it in its original location as accurately as possible.  You will hear noises upon successfully lifting and replacing the object. Then, return to the starting location with your fingers pinched to end the trial.  The countdown for the following trial will begin after around one second. For each movement, move as quickly as you can while ensuring a consistently high level of accuracy. Please do not slam the object down or replace it forcefully. This task will last for 18 trials.", "manual_reaching_task")
text_to_wav("en-US-Wavenet-C", "You should begin with the tips of your thumb and index finger pinched together within the small green cube at the edge of the table closest to you.", "manual_reaching_task_2")
# Pre Tool-use Audio
text_to_wav("en-US-Wavenet-C", "On the table in front of you is a grabber tool. Please pick the tool up by the handle. Once you have the tool in hand, squeeze the handle a couple of times to get a sense for how it works before holding it in the position displayed on the canvas in front of you.","pre_tool_use_task")

# Tool-use Task Audio
text_to_wav("en-US-Wavenet-C", "In the upcoming task, the goal is to use a grabber tool to accurately grasp, lift and replace the tracked object in front of you on consecutive trials. You should begin with the tips of the tool’s prongs pinched together within the small green cube at the edge of the table to your right. Each trial will begin with a three second countdown, with the final tone signaling that you should begin.   When the trial begins, reach out and pick up the object from the sides. Aim for the centers of the left and right faces of the object. Once you have grasped the object, lift it from the table to touch the green area before replacing it in its original location as accurately as possible. You will hear noises upon successfully lifting and replacing the object. Once the object is replaced, return the prongs of the tool to their pinched position in the starting area. The countdown for the following trial will begin after around one second. Move as quickly as you can while still maintaining a high level of accuracy. This task will last for 48 trials. This task is self-paced, and you may take breaks if you begin to feel fatigued. To take a break, simply move the tool out of the starting location after finishing a trial. The task will pause until you return to the starting position.", "tool_use_task")
text_to_wav("en-US-Wavenet-C",  "You should begin with the tips of your left index finger and thumb, as well as the tips of the tool’s prongs, pinched together within the small green spaces at the edge of the table.", "tool-use-task")
text_to_wav("en-US-Wavenet-C", "If you move the prongs of the tool or the pinched fingers of your left hand out of the starting areas before the countdown ends, it will restart.", "tool-use-task_2")
text_to_wav("en-US-Wavenet-C", "Move as quickly as you can, while ensuring a consistently high level of accuracy.", "tool-use-task_3")

# Shared End Audio
text_to_wav("en-US-Wavenet-C", "That was the last trial of the block. Please wait as the next task is prepared.", "end_of_block")

# End Landmark Audio
text_to_wav("en-US-Wavenet-C", "You can now lift your arm from the table.", "after_landmark_task")

# End Tool-use Audio
text_to_wav("en-US-Wavenet-C", "Thank you for your perseverance. Please place the tool on the table in front of you and wait as the next task is prepared.", "after_tool_use_task")

# End of Study
text_to_wav("en-US-Wavenet-C","That concludes the Virtual Reality portion of the study. Thank you for your perseverance. Please take off the headset and allow the researcher to assist you with removing the trackers and gloves.", "end_of_study")


# Wrist Pivot Calibration Audio
text_to_wav("en-US-Wavenet-C", "The next stage of the study will attempt to calibrate the location of your virtual wrist to be in perfect alignment with your real wrist. A cube with a tracker on top of it is now on the table in front of you. Please grasp the cube with your right hand, and then remain stationary as the researcher adjusts your hand location. A tone will signal that the calibration has finished.", "manual_wrist_calibration")
text_to_wav("en-US-Wavenet-C", "Excellent job. Now, please repeat the same process with your left hand.", "wrist_calibration_pt2")
text_to_wav("en-US-Wavenet-C", "The next stage of the study will attempt to calibrate the location of your virtual wrist to be in perfect alignment with your real wrist. Please hold your right forearm just under the wrist joint with your left hand and attempt to keep it as stationary as possible. Once you hear a tone, begin moving your hand front to back, left to right, and in circles, stopping when you hear a second tone.", "wrist_pivot_calibration")
text_to_wav("en-US-Wavenet-C", "Excellent job. Now, please repeat the same process with your left hand.", "wrist_calibration_pt2")
text_to_wav("en-US-Wavenet-C", "Excellent job. Now, please move your left hand just below the elbow, and attempt to keep it stationary. Once your hear a tone, alternate between making a hammering motion, rotating your forearm and pivoting it left and right.", "elbow_pivot_calibration")
text_to_wav("en-US-Wavenet-C", "Please place your right hand palm down on the table in front of you. The researcher will now calibrate the wrist pivot.", "wrist_pivot_calibration")

text_to_wav("en-US-Wavenet-C", "Calibration started.", "target_calibration_start", False, "target_calibration")
text_to_wav("en-US-Wavenet-C", "Calibrating...", "target_calibration_underway", False, "target_calibration")
text_to_wav("en-US-Wavenet-C", "Calibration finished. Please validate.", "target_calibration_finish", False, "target_calibration")
text_to_wav("en-US-Wavenet-C", "Calibration saved.", "target_calibration_saved", False, "target_calibration")
text_to_wav("en-US-Wavenet-C", "Wrist calibration.", "target_calibration_wrist", False, "target_calibration")
text_to_wav("en-US-Wavenet-C", "Elbow calibration.", "target_calibration_elbow", False, "target_calibration")

