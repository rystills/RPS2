<?php
// get roomCode/message
$roomCode = $_POST['room'] ?? '';
$message = $_POST['message'] ?? '';

// validate
if (empty($roomCode) || empty($message))
    exit;

// append message to room file
$roomFile = 'rooms/' . $roomCode . '.txt';
file_put_contents($roomFile, $message.PHP_EOL, FILE_APPEND);
?>
