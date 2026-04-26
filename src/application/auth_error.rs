#[derive(Debug)]
pub enum AuthError {
    UserNotFound,
    InvalidCredentials,
    VerificationFailed,
}
