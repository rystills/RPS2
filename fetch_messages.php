<?php
// get roomCode
$roomCode = $_GET['room'] ?? '';

// validate
if (empty($roomCode))
{
    echo '';
    exit;
}

// return messages from room file
$roomFile = 'rooms/' . $roomCode . '.txt';
if (file_exists($roomFile))
{
    $messages = file_get_contents($roomFile);
    echo $messages;
}
else echo '';
?>
